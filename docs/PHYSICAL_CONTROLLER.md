# Controle físico ESP32-S3 + MPU6050 + encoder

## Lista de materiais

| Quantidade | Componente | Observação |
|---:|---|---|
| 1 | ESP32-S3 DevKitC-1 ou compatível | USB CDC, alimentação pelo computador |
| 1 | GY-521/MPU6050 | IMU econômica 6DoF, endereço I2C `0x68` |
| 1 | KY-040 ou encoder EC11 de 20 pulsos | Medição por quadratura |
| 1 | Roda impressa + O-ring 20 x 2 mm | Mantém contato com a vareta |
| 2 | Botões momentâneos normalmente abertos | Ação e calibração |
| 1 | LED + resistor de 220 ohms | Opcional se a placa não tiver LED utilizável |
| 1 | Vareta lisa de 10 mm | Apenas simulador de mesa; nunca inserir em pessoa |
| 1 | Mola pequena de compressão | Pressão da roda sobre a vareta |
| 3 | Parafusos M3, porcas e arruelas | Pivô e ajuste do encoder |
| 4 | Parafusos M4 | Fixação da base à mesa |
| 1 | Cabo USB de dados | Não usar cabo somente de carga |

O valor inicial de `0,785 mm/tick` considera roda efetiva de 20 mm e encoder de 20 pulsos contado nas quatro transições. Sempre execute a calibração real de 100 mm porque diâmetro, O-ring e encoder variam.

## Ligações

| ESP32-S3 | Componente |
|---|---|
| 3V3 | VCC do GY-521 |
| GND | GND comum, encoder e botões |
| GPIO 8 | SDA do GY-521 |
| GPIO 9 | SCL do GY-521 |
| GPIO 4 | Canal A/CLK do encoder |
| GPIO 5 | Canal B/DT do encoder |
| GPIO 6 | Botão de ação para GND |
| GPIO 7 | Botão de calibração para GND |

Não alimente o GY-521 com 5 V quando os resistores de pull-up I2C da placa não forem confirmados para 3,3 V. O firmware usa `INPUT_PULLUP` para encoder e botões.

## Firmware

O código está em `hardware/firmware/ureteroscope_controller` e pode ser aberto diretamente na Arduino IDE. Instale pelo Library Manager:

- `Adafruit MPU6050` e dependências;
- `Madgwick` da Arduino Libraries;
- pacote de placas `esp32` da Espressif.

Selecione uma placa ESP32-S3, habilite USB CDC ao iniciar, compile e grave. O arquivo `platformio.ini` permite a alternativa PlatformIO. A inicialização exige dois segundos com o controle imóvel para estimar o viés do giroscópio.

O firmware transmite JSON delimitado por nova linha a 115200 baud e 50 Hz:

```json
{"v":1,"seq":1,"ms":100,"q":[1,0,0,0],"ticks":20,"buttons":0,"imu_ok":true,"fw":"mpu6050-encoder-v1.0.0"}
```

O quaternion usa ordem `[w,x,y,z]`. `buttons` usa bit 0 para ação e bit 1 para calibração. O Unity rejeita versões, quaternions ou mensagens inválidas.

## Impressão e montagem

O modelo paramétrico está em `hardware/cad/ureteroscope_controller.scad`. Parâmetros principais: `rod_diameter`, `print_tolerance` e `encoder_shaft_diameter`. Exporte os seis STL com:

```powershell
.\hardware\cad\export_stl.ps1
```

Configuração inicial: PETG ou PLA, camada de 0,2 mm, quatro perímetros e 25% de preenchimento. Rebarbe a bucha sem aumentar folga excessivamente. Coloque um O-ring na roda, monte o braço com M3 e use a mola/parafuso para obter contato sem travar a vareta. Fixe a IMU rigidamente no cabo, com os eixos sempre na mesma orientação.

## Calibração e aceite

1. Prenda a base à mesa e confirme que a vareta desliza sem folga lateral perceptível.
2. No jogo selecione `Vareta USB`, deixe a porta em `AUTO` ou informe `COM3`, por exemplo.
3. Clique `Calibrar encoder 100 mm`, marque o zero, avance exatamente 100 mm e conclua. A escala fica salva no computador.
4. Inicie uma sessão, mantenha a vareta imóvel e calibre a orientação. Repita a recentralização entre tentativas.
5. Faça cinco ciclos de 200 mm. Cada leitura deve ter erro absoluto máximo de 5 mm e não pode perder contato.
6. Verifique resposta visual abaixo de 100 ms e deriva parada menor que 10 graus em três minutos.

O MPU6050 não possui magnetômetro, acumula deriva de rumo e foi descontinuado pelo fabricante. Módulos GY-521 podem usar componentes clones. Esta escolha é adequada somente à prova de conceito; a interface do Unity permite uma futura troca por BNO085 sem mudar as regras do jogo.
