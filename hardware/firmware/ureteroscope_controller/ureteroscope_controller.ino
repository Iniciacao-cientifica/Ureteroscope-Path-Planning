#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>
#include <MadgwickAHRS.h>
#include <Wire.h>

// ESP32-S3 DevKit defaults. Change only if the selected board routes I2C elsewhere.
constexpr uint8_t PIN_SDA = 8;
constexpr uint8_t PIN_SCL = 9;
constexpr uint8_t PIN_ENCODER_A = 4;
constexpr uint8_t PIN_ENCODER_B = 5;
constexpr uint8_t PIN_ACTION = 6;
constexpr uint8_t PIN_CALIBRATE = 7;
#ifdef LED_BUILTIN
constexpr uint8_t PIN_STATUS_LED = LED_BUILTIN;
#else
constexpr uint8_t PIN_STATUS_LED = 48;
#endif

constexpr float SAMPLE_HZ = 100.0f;
constexpr uint32_t SAMPLE_PERIOD_US = 1000000UL / static_cast<uint32_t>(SAMPLE_HZ);
constexpr uint32_t SEND_PERIOD_MS = 20;
constexpr uint32_t GYRO_CALIBRATION_MS = 2000;
constexpr char FIRMWARE_VERSION[] = "mpu6050-encoder-v1.0.0";

Adafruit_MPU6050 mpu;
Madgwick filter;
volatile int32_t encoderTicks = 0;
volatile uint8_t previousEncoderState = 0;
float gyroBiasX = 0.0f;
float gyroBiasY = 0.0f;
float gyroBiasZ = 0.0f;
bool imuOk = false;
uint32_t sequenceNumber = 0;
uint32_t lastSampleUs = 0;
uint32_t lastSendMs = 0;
bool previousCalibrationButton = false;

void IRAM_ATTR updateEncoder() {
  const uint8_t current = (digitalRead(PIN_ENCODER_A) << 1) | digitalRead(PIN_ENCODER_B);
  // Quadrature lookup table; rejects impossible two-bit jumps caused by bounce.
  static constexpr int8_t transitions[16] = {
    0, -1, 1, 0,
    1, 0, 0, -1,
    -1, 0, 0, 1,
    0, 1, -1, 0
  };
  encoderTicks += transitions[(previousEncoderState << 2) | current];
  previousEncoderState = current;
}

void setStatusLed(bool on) {
  digitalWrite(PIN_STATUS_LED, on ? HIGH : LOW);
}

void calibrateGyroscope() {
  if (!imuOk) return;
  setStatusLed(true);
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
  setStatusLed(false);
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
  float w;
  float x;
  float y;
  float z;
  eulerDegreesToQuaternion(filter.getRoll(), filter.getPitch(), filter.getYaw(), w, x, y, z);
  const bool actionPressed = digitalRead(PIN_ACTION) == LOW;
  const bool calibratePressed = digitalRead(PIN_CALIBRATE) == LOW;
  const uint8_t buttons = (actionPressed ? 1 : 0) | (calibratePressed ? 2 : 0);
  int32_t stableTicks;
  noInterrupts();
  stableTicks = encoderTicks;
  interrupts();

  Serial.printf(
    "{\"v\":1,\"seq\":%lu,\"ms\":%lu,\"q\":[%.6f,%.6f,%.6f,%.6f],"
    "\"ticks\":%ld,\"buttons\":%u,\"imu_ok\":%s,\"fw\":\"%s\"}\n",
    static_cast<unsigned long>(sequenceNumber++),
    static_cast<unsigned long>(millis()),
    w, x, y, z,
    static_cast<long>(stableTicks),
    buttons,
    imuOk ? "true" : "false",
    FIRMWARE_VERSION
  );
}

void setup() {
  pinMode(PIN_ENCODER_A, INPUT_PULLUP);
  pinMode(PIN_ENCODER_B, INPUT_PULLUP);
  pinMode(PIN_ACTION, INPUT_PULLUP);
  pinMode(PIN_CALIBRATE, INPUT_PULLUP);
  pinMode(PIN_STATUS_LED, OUTPUT);
  previousEncoderState = (digitalRead(PIN_ENCODER_A) << 1) | digitalRead(PIN_ENCODER_B);
  attachInterrupt(digitalPinToInterrupt(PIN_ENCODER_A), updateEncoder, CHANGE);
  attachInterrupt(digitalPinToInterrupt(PIN_ENCODER_B), updateEncoder, CHANGE);

  Serial.begin(115200);
  Wire.begin(PIN_SDA, PIN_SCL);
  imuOk = mpu.begin(0x68, &Wire);
  if (imuOk) {
    mpu.setAccelerometerRange(MPU6050_RANGE_4_G);
    mpu.setGyroRange(MPU6050_RANGE_500_DEG);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
    filter.begin(SAMPLE_HZ);
    calibrateGyroscope();
  } else {
    setStatusLed(true);
  }
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
      acceleration.acceleration.z
    );
  }

  const bool calibrationButton = digitalRead(PIN_CALIBRATE) == LOW;
  if (calibrationButton && !previousCalibrationButton) calibrateGyroscope();
  previousCalibrationButton = calibrationButton;

  const uint32_t nowMs = millis();
  if (nowMs - lastSendMs >= SEND_PERIOD_MS) {
    lastSendMs += SEND_PERIOD_MS;
    sendPacket();
  }
}
