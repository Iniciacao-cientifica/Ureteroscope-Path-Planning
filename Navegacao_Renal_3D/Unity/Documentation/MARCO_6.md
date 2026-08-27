# Navegação Renal 3D — Marco 6

## Resultado

O modo Realista aceita duas fontes selecionadas no menu: mouse/teclado ou
ESP32/MPU. A Exploração continua exclusivamente no mouse. Toda a física
`SphereCast`, o nível fácil, a pedra, a garra virtual e as geometrias v003
permanecem inalteradas.

## Controle com MPU

- o MPU6050 controla a orientação relativa da ponta;
- a pose recebida ao iniciar é adotada como posição neutra;
- `C` recalibra a posição neutra;
- segurar o botão movimenta na direção indicada pelo HUD;
- clique duplo em até `350 ms` alterna Avanço/Recuo;
- dentro de `0,10 m` da pedra, o botão deixa de mover e passa a fechar a garra;
- a captura exige um segundo contínuo e é cancelada ao soltar ou afastar.

O MPU6050 não tem magnetômetro. Uma deriva lenta de yaw é esperada e pode ser
corrigida imediatamente com `C`.

## USB e recuperação

O Unity recebe JSON v2 em `115200 baud` por uma thread de fundo. A implementação
usa a API serial nativa do Windows, sem DLL externa. A porta pode ser detectada
automaticamente ou escolhida na lista COM.

Após `250 ms` sem pacote válido, a tentativa é pausada, a captura é cancelada e
o painel de reconexão aparece. O jogo retoma do mesmo ponto quando dados válidos
voltam. Pacotes inválidos, duplicados ou fora de ordem são descartados.

## Firmware

- placa: ESP32 DevKit V1 (`esp32dev`);
- SDA 21, SCL 22 e botão 25;
- MPU amostrado a `100 Hz`;
- filtro Madgwick;
- calibração inicial de giroscópio por dois segundos;
- pacotes enviados a `50 Hz`.

As instruções de montagem e gravação estão em
`hardware/firmware/ureteroscope_controller/README.md`.

## Validação

- Unity `6000.5.0f1` sem erros de compilação ou scripts ausentes;
- `133` verificações anteriores reexecutadas;
- `40` verificações específicas do Marco 6;
- `173` verificações aprovadas no total;
- parser, quaternion, calibração, zona morta e replay serial testados;
- orientação comparada em 30, 60 e 120 FPS;
- firmware compilado com sucesso para ESP32 DevKit V1;
- hashes de v002, v003 e Meshy preservados.

O relatório está em `Documentation/marco6_validation.json`. A validação de
hardware é declarada como simulação/replay porque a montagem física ainda não
está disponível.

## Build Windows

O comando `Navegacao Renal > Gerar Build Windows Marco 6` gera:

- pasta `Navegacao_Renal_3D_Unity_Build_Marco6` na Área de Trabalho;
- executável `Navegacao_Renal_3D.exe`;
- ZIP portátil ao lado da pasta;
- manifesto `Documentation/marco6_build.json`.

O build é Windows x86-64, 1920×1080 em janela sem bordas. Não inclui Quest,
encoder, servo, sensor de pedra ou alterações anatômicas do futuro Marco 5.1.
