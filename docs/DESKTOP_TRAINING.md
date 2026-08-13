# Treinador desktop com vareta física

Protótipo acadêmico/educacional para navegação em uma máscara anatômica revisada. Não é um dispositivo médico e não deve ser usado em pacientes, procedimentos clínicos, diagnóstico, navegação intraoperatória ou controle de equipamentos.

## O que já funciona

- Visão endoscópica na ponta virtual e minimapa externo.
- Casos, pedras e rotas do pipeline v2 já existente.
- Tutorial com rota interna, intermediário com rota somente no minimapa e avançado sem rota.
- Controle por teclado/mouse ou ESP32-S3 via USB.
- Colisões, tempo, desvio RMS, eficiência, pontuação e resultado CSV anônimo.
- Pausa após 500 ms sem pacotes do controle.

## Abrir e testar

No Unity 6000.5 use `Murillo VR > Setup Desktop Training Scene` e abra `Assets/Scenes/UreteroscopyDesktopTraining.unity`. A cena já está versionada, portanto normalmente basta pressionar Play.

No modo teclado:

- `W/S`: avanço e recuo;
- mouse ou setas: inclinação e direção;
- `Q/E`: rotação axial;
- `Espaço`: botão de ação;
- `C`: calibração/recentralização.

Para gerar o executável use `Murillo VR > Build Desktop Training (Windows)`. O resultado fica em `UnityVRPrototype/Builds/Desktop/UreteroscopyTraining.exe`.

## Regras e dados

A sessão termina quando a ponta permanece por 0,5 s a no máximo `máx(raio da pedra + 5 mm, 8 mm)`, aponta para o alvo com erro de até 15 graus e o usuário aciona o gatilho. Avanço contra a parede é bloqueado; recuo continua permitido.

A nota combina segurança (40), precisão (30), eficiência (20) e tempo (10). Sessões interrompidas são registradas como `DNF` e não recebem nota. O CSV fica em `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/Sessions/ureteroscopy_sessions.csv` e contém apenas código escolhido pelo pesquisador, caso, rota, dificuldade e métricas. Não digite nome, CPF, prontuário ou outro identificador pessoal.

## Limitação anatômica

O início é o ponto de entrada humano-revisado da máscara disponível. A cena não afirma reproduzir todo o percurso externo, uretra, bexiga, ureter e rim. Uma simulação contínua dessas estruturas depende de dados segmentados e validados que ainda não estão no repositório.
