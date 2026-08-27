# Controle físico — Marco 6

Firmware para `ESP32 DevKit V1`, um `MPU6050` e um botão normalmente aberto.
Não há encoder, servo ou sensor de captura nesta versão.

## Ligações

| Componente | ESP32 DevKit V1 |
|---|---:|
| MPU6050 VCC | 3V3 |
| MPU6050 GND | GND |
| MPU6050 SDA | GPIO 21 |
| MPU6050 SCL | GPIO 22 |
| Botão terminal 1 | GPIO 25 |
| Botão terminal 2 | GND |

O botão usa `INPUT_PULLUP`: solto é nível alto e pressionado é nível baixo.

## Compilar e gravar

Instale o PlatformIO e, nesta pasta, execute:

```powershell
pio run
pio run --target upload
pio device monitor --baud 115200
```

Ao ligar, mantenha o MPU imóvel por dois segundos. O monitor mostrará as
mensagens de inicialização e depois apenas pacotes JSON v2 a `50 Hz`.

## Protocolo

```json
{"v":2,"seq":123,"ms":4567,"q":[1.0,0.0,0.0,0.0],"button":false,"imu_ok":true,"fw":"mpu6050-button-v2.0.0"}
```

- `q`: quaternion na ordem `w, x, y, z`;
- `button`: estado do botão físico;
- `imu_ok`: inicialização válida do MPU6050;
- `seq`: sequência usada pelo Unity para rejeitar duplicatas.

O firmware foi compilado para `esp32dev`. O ensaio elétrico real permanece
pendente até a montagem do hardware.
