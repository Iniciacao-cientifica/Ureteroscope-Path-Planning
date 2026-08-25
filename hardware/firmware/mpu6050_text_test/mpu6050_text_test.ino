#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>

#define MPU_ADDR 0x68
#define BTN_PIN 25

Adafruit_MPU6050 mpu;

unsigned long lastSend = 0;
const unsigned long SEND_INTERVAL = 700;

void setup() {
  Serial.begin(115200);
  delay(200);

  Wire.begin(21, 22);
  pinMode(BTN_PIN, INPUT_PULLUP);

  Serial.println("Inicializando MPU6050...");
  if (!mpu.begin(MPU_ADDR)) {
    Serial.println("ERRO: MPU6050 nao encontrado! Verifique a fiacao (SDA=D21, SCL=D22).");
    while (1) delay(10);
  }

  mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
  mpu.setGyroRange(MPU6050_RANGE_500_DEG);
  mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);

  Serial.println("Sensor pronto!");
  Serial.println("Mantenha o Monitor Serial fechado enquanto o Unity estiver conectado.");
  delay(500);
}

void loop() {
  if (millis() - lastSend >= SEND_INTERVAL) {
    lastSend = millis();
    exibirLeituras();
  }
}

void exibirLeituras() {
  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  bool btn = !digitalRead(BTN_PIN);

  Serial.println(F("========================================"));
  Serial.println(F(">> PINCA"));
  Serial.print(F("   Aceleracao (m/s^2):  X=")); Serial.print(a.acceleration.x, 2);
  Serial.print(F("  Y=")); Serial.print(a.acceleration.y, 2);
  Serial.print(F("  Z=")); Serial.println(a.acceleration.z, 2);
  Serial.print(F("   Giroscopio (rad/s):  X=")); Serial.print(g.gyro.x, 2);
  Serial.print(F("  Y=")); Serial.print(g.gyro.y, 2);
  Serial.print(F("  Z=")); Serial.println(g.gyro.z, 2);
  Serial.print(F("   Agarrando: ")); Serial.println(btn ? F("SIM") : F("nao"));
  Serial.println(F("========================================\n"));
}
