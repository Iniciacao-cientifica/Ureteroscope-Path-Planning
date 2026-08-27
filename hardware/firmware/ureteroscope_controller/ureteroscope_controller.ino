#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>
#include <MadgwickAHRS.h>
#include <Wire.h>

// Marco 6 hardware: ESP32 DevKit V1 + MPU6050 + one active-low action button.
constexpr uint8_t PIN_SDA = 21;
constexpr uint8_t PIN_SCL = 22;
constexpr uint8_t PIN_ACTION = 25;

constexpr float SAMPLE_HZ = 100.0f;
constexpr uint32_t SAMPLE_PERIOD_US = 1000000UL / static_cast<uint32_t>(SAMPLE_HZ);
constexpr uint32_t SEND_PERIOD_MS = 20;  // 50 Hz USB stream.
constexpr uint32_t GYRO_CALIBRATION_MS = 2000;
constexpr char FIRMWARE_VERSION[] = "mpu6050-button-v2.0.0";

Adafruit_MPU6050 mpu;
Madgwick filter;
float gyroBiasX = 0.0f;
float gyroBiasY = 0.0f;
float gyroBiasZ = 0.0f;
bool imuOk = false;
uint32_t sequenceNumber = 0;
uint32_t lastSampleUs = 0;
uint32_t lastSendMs = 0;

void calibrateGyroscope() {
  if (!imuOk) return;
  Serial.println("CALIBRATING_MPU_KEEP_STILL");
  double sumX = 0.0;
  double sumY = 0.0;
  double sumZ = 0.0;
  uint32_t count = 0;
  const uint32_t startedAt = millis();
  while (millis() - startedAt < GYRO_CALIBRATION_MS) {
    sensors_event_t acceleration;
    sensors_event_t gyro;
    sensors_event_t temperature;
    mpu.getEvent(&acceleration, &gyro, &temperature);
    sumX += gyro.gyro.x;
    sumY += gyro.gyro.y;
    sumZ += gyro.gyro.z;
    count++;
    delay(4);
  }
  if (count > 0) {
    gyroBiasX = static_cast<float>(sumX / count);
    gyroBiasY = static_cast<float>(sumY / count);
    gyroBiasZ = static_cast<float>(sumZ / count);
  }
  filter = Madgwick();
  filter.begin(SAMPLE_HZ);
  lastSampleUs = micros();
  Serial.println("MPU_READY_JSON_V2");
}

void eulerDegreesToQuaternion(float rollDegrees, float pitchDegrees, float yawDegrees,
                              float &w, float &x, float &y, float &z) {
  const float roll = rollDegrees * DEG_TO_RAD * 0.5f;
  const float pitch = pitchDegrees * DEG_TO_RAD * 0.5f;
  const float yaw = yawDegrees * DEG_TO_RAD * 0.5f;
  const float cr = cosf(roll);
  const float sr = sinf(roll);
  const float cp = cosf(pitch);
  const float sp = sinf(pitch);
  const float cy = cosf(yaw);
  const float sy = sinf(yaw);
  w = cr * cp * cy + sr * sp * sy;
  x = sr * cp * cy - cr * sp * sy;
  y = cr * sp * cy + sr * cp * sy;
  z = cr * cp * sy - sr * sp * cy;
}

void sendPacket() {
  float w = 1.0f;
  float x = 0.0f;
  float y = 0.0f;
  float z = 0.0f;
  if (imuOk) {
    eulerDegreesToQuaternion(filter.getRoll(), filter.getPitch(), filter.getYaw(), w, x, y, z);
  }
  const bool actionPressed = digitalRead(PIN_ACTION) == LOW;
  Serial.printf(
      "{\"v\":2,\"seq\":%lu,\"ms\":%lu,\"q\":[%.6f,%.6f,%.6f,%.6f],"
      "\"button\":%s,\"imu_ok\":%s,\"fw\":\"%s\"}\n",
      static_cast<unsigned long>(sequenceNumber++),
      static_cast<unsigned long>(millis()),
      w, x, y, z,
      actionPressed ? "true" : "false",
      imuOk ? "true" : "false",
      FIRMWARE_VERSION);
}

void setup() {
  Serial.begin(115200);
  delay(200);
  pinMode(PIN_ACTION, INPUT_PULLUP);
  Wire.begin(PIN_SDA, PIN_SCL);
  Serial.println("INITIALIZING_MPU6050");
  imuOk = mpu.begin(0x68, &Wire);
  if (!imuOk) {
    Serial.println("ERROR_MPU6050_NOT_FOUND_SDA21_SCL22");
    return;
  }
  mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
  mpu.setGyroRange(MPU6050_RANGE_500_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
  filter.begin(SAMPLE_HZ);
  calibrateGyroscope();
}

void loop() {
  const uint32_t nowUs = micros();
  if (imuOk && nowUs - lastSampleUs >= SAMPLE_PERIOD_US) {
    lastSampleUs += SAMPLE_PERIOD_US;
    sensors_event_t acceleration;
    sensors_event_t gyro;
    sensors_event_t temperature;
    mpu.getEvent(&acceleration, &gyro, &temperature);
    filter.updateIMU(
        (gyro.gyro.x - gyroBiasX) * RAD_TO_DEG,
        (gyro.gyro.y - gyroBiasY) * RAD_TO_DEG,
        (gyro.gyro.z - gyroBiasZ) * RAD_TO_DEG,
        acceleration.acceleration.x,
        acceleration.acceleration.y,
        acceleration.acceleration.z);
  }
  const uint32_t nowMs = millis();
  if (nowMs - lastSendMs >= SEND_PERIOD_MS) {
    lastSendMs += SEND_PERIOD_MS;
    sendPacket();
  }
}
