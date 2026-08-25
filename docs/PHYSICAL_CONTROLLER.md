# Controle experimental ESP32 + MPU6050

## Lista de materiais

| Quantidade | Componente | Observação |
|---:|---|---|
| 1 | ESP32 ou compatível | USB serial, alimentação pelo computador |
| 1 | GY-521/MPU6050 | IMU econômica 6DoF, endereço I2C `0x68` |
| 1 | Botão momentâneo normalmente aberto | Ação sobre a pedra |
| 1 | Cabo USB de dados | Não usar cabo somente de carga |

Este modo é destinado a um teste rápido. A inclinação simula avanço/recuo e não substitui um encoder quando for necessária medição física de deslocamento.

## Ligações do teste MPU6050 textual

| ESP32 | Componente |
|---|---|
| 3V3 | VCC do GY-521 |
| GND | GND comum do GY-521 e botão |
| GPIO 21 | SDA do GY-521 |
| GPIO 22 | SCL do GY-521 |
| GPIO 25 | Botão de ação para GND |

Não alimente o GY-521 com 5 V quando os resistores de pull-up I2C da placa não forem confirmados para 3,3 V. O botão usa `INPUT_PULLUP` e deve fechar para GND.

## Firmware

O código experimental está em `hardware/firmware/mpu6050_text_test` e pode ser aberto diretamente na Arduino IDE. Instale pelo Library Manager:

- `Adafruit MPU6050` e dependências;
- pacote de placas `esp32` da Espressif.

Selecione a placa ESP32 correspondente, compile e grave. O Unity abre a porta a 115200 baud e reconhece blocos como:

```text
Aceleracao (m/s^2): X=0.00 Y=0.00 Z=9.81
Giroscopio (rad/s): X=0.00 Y=0.00 Z=0.00
Agarrando: nao
```

A inclinação longitudinal controla avanço e recuo; o botão confirma a ação. Esse teste não utiliza encoder e não mede deslocamento físico real.

## Impressão e montagem

O modelo paramétrico está em `hardware/cad/ureteroscope_controller.scad`. Parâmetros principais: `rod_diameter`, `print_tolerance` e `encoder_shaft_diameter`. Exporte os seis STL com:

```powershell
.\hardware\cad\export_stl.ps1
```

Configuração inicial: PETG ou PLA, camada de 0,2 mm, quatro perímetros e 25% de preenchimento. Rebarbe a bucha sem aumentar folga excessivamente. Coloque um O-ring na roda, monte o braço com M3 e use a mola/parafuso para obter contato sem travar a vareta. Fixe a IMU rigidamente no cabo, com os eixos sempre na mesma orientação.

## Calibração e aceite

1. Prenda a base à mesa e confirme que a vareta desliza sem folga lateral perceptível.
2. No jogo selecione `MPU6050 USB`, deixe a porta em `AUTO` ou informe `COM3`, por exemplo.
3. Inicie uma sessão, mantenha a vareta imóvel e clique `CALIBRAR AGORA`.
4. Incline para frente e para trás; use `INVERTER AVANÇO` no menu se o sentido estiver trocado.
5. Alinhe a pedra e pressione o botão para concluir.
6. Confirme que o jogo pausa se o ESP32 ficar mais de dois segundos sem enviar uma amostra completa.

O MPU6050 não possui magnetômetro, acumula deriva de rumo e foi descontinuado pelo fabricante. Módulos GY-521 podem usar componentes clones. Esta escolha é adequada somente à prova de conceito; a interface do Unity permite uma futura troca por BNO085 sem mudar as regras do jogo.
