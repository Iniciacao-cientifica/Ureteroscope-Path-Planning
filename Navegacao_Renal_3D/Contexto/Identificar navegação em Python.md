# Contexto da conversa — Identificar navegação em Python

Última atualização: 26 de agosto de 2026
Conversa Codex: `01a0398f-fbb5-7a40-a640-68e7e5a9d9cc`
Repositório: `Ureteroscope-Path-Planning`
Branch de desenvolvimento do zero: `DoZero`

## Finalidade deste documento

Este arquivo preserva as decisões da conversa que transformou o visualizador
Python existente em um plano para o jogo/protótipo acadêmico **Navegação Renal
3D**. Ele deve ser consultado antes de alterar modelo, Unity, controles ou
integração física.

O texto registra:

- as 57 perguntas de definição e as respostas dadas;
- correções e decisões que substituíram respostas anteriores;
- o escopo atual do modelo renal;
- os dois modos de jogo;
- os caminhos oficiais dos artefatos Maya e Unity;
- o estado dos marcos;
- as necessidades futuras de ESP32, MPU6050, encoder e garra.

Quando houver conflito, a seção **Decisões vigentes** prevalece sobre respostas
antigas do questionário.

## Origem: navegação existente em Python

A conversa começou com a pergunta: **“sabe me dizer o que é usado no código de
python para criar aquela navegação?”**

O projeto Python existente utiliza três partes distintas:

- `path_planning.py`: cálculo da rota em voxels com algoritmo A* e campo de
  repulsão das paredes;
- `main.py`: suavização da rota por B-Spline, Savitzky–Golay e técnicas
  laplacianas;
- `view.py`: visualização/animação da câmera com PyVista, baseado em VTK, e
  NumPy para posição e orientação.

Essa animação Python não é o jogo. No novo projeto, a rota é fixa e será um
asset do Unity; não haverá A* durante a partida.

## Objetivo consolidado

Criar do zero um jogo sério para Windows, inicialmente controlado por mouse e
teclado, com um percurso renal fixo até uma pedra. A arquitetura deverá aceitar
hardware posteriormente sem reescrever a jogabilidade.

O projeto sempre usa:

- a mesma anatomia;
- o mesmo rim ativo e sistema coletor navegável;
- o mesmo ponto inicial no ureter;
- a mesma rota;
- a mesma pedra em um cálice médio;
- apenas o nível fácil nesta primeira versão.

O produto é acadêmico/educacional. Não é dispositivo médico, reconstrução de
paciente ou anatomia clinicamente validada.

## Questionário completo — perguntas 1 a 57

### Rodada 1 — plataforma, anatomia e derrota

1. **Onde deve funcionar primeiro?**
   Resposta: **A — Windows com ESP32 por USB.** Depois o hardware foi adiado;
   Windows continua sendo a plataforma inicial.

2. **Qual representação anatômica você quer?**
   Resposta: **A — Rim externo e sistema coletor separados.**

3. **Como deve funcionar a derrota por colisões?**
   Resposta: **A — Limite configurável, começando em cinco colisões.**

### Rodada 2 — controle físico e visualização

4. **Qual é o estado atual do controle físico?**
   Resposta: **B — Já existem ESP32 e MPU, mas ainda não há haste com encoder.**

5. **Quando você fala em “garra”, o que exatamente deseja controlar?**
   Resposta livre: segurar um botão para a garra pegar a pedra. A intenção final
   é existir uma pedrinha real. Isso corresponde a abrir/fechar uma pinça; a
   articulação distal não foi exigida nesta etapa.

6. **Como você imagina a visualização durante a partida?**
   Resposta: **B — Visão interna com minimapa externo.**

### Rodada 3 — pedra física e destino de visualização

7. **O que você imagina por “pedrinha real”?**
   Resposta: **A — Pedra física dentro de um rim físico, capturada por garra
   física.** Requisito futuro, depois do protótipo com mouse.

8. **Quando o jogador captura a pedra, quando deve ganhar?**
   Resposta: **A — Imediatamente ao fechar a garra corretamente sobre a pedra.**

9. **Qual é o objetivo final de visualização?**
   Resposta: **B — Windows e Meta Quest conectado por Link/Air Link.** A primeira
   versão permanece somente desktop.

### Rodada 4 — mecânica física, suspensa

10. **Como deve ser a haste do instrumento?**
    Não respondida. A discussão de hardware foi suspensa para começar o Unity
    do zero com mouse e teclado.

11. **Como o botão deve fechar a garra física?**
    Não respondida. A sugestão registrada foi servo na empunhadura puxando um
    cabo, mas isso não é decisão final.

12. **Como o jogo deve confirmar que a pedra foi realmente capturada?**
    Não respondida. Sensores físicos permanecem para uma fase futura.

### Rodada 5 — novo Unity e controles provisórios

13. **Como começaremos o projeto?**
    Resposta: **A — Projeto Unity 6 URP completamente novo e separado.** Não
    reutilizar o Unity antigo como base.

14. **Como será o controle no modo realista?**
    Resposta: **A — Mouse orienta e W/S avança ou recua.**

15. **Como será o controle no modo exploração?**
    Resposta: **B — Câmera livre com WASD, mouse e Q/E para subir/descer.**

### Rodada 6 — anatomia e apresentação

16. **Qual material anatômico utilizaremos inicialmente?**
    A resposta foi substituída por uma pergunta do usuário: criar um rim do zero
    ou adaptar um pronto. A decisão posterior, na pergunta 34, foi criar todo o
    modelo do zero para o jogo.

17. **Qual estilo visual você deseja?**
    Resposta: **B — Realista, mas com elementos de jogo.**

18. **Como deseja o minimapa no modo realista?**
    Resposta: **A — Pequeno no canto superior direito.**

### Rodada 7 — exterior, dificuldade e captura

19. **Como o rim exterior deve aparecer no modo exploração?**
    Resposta: **B — Sempre semitransparente.** A cena deverá também oferecer
    controles para mostrar, ocultar ou revisar o exterior.

20. **Como a rota deve aparecer no modo realista?**
    Resposta original: **B — Três dificuldades.** Decisão posterior vigente:
    somente o nível fácil, com rota ciano visível.

21. **Como funcionará a captura provisória, antes da garra física?**
    Resposta: **A — Encostar na pedra e pressionar Espaço.**

### Rodada 8 — parede, feedback e iluminação

22. **O que deve acontecer fisicamente ao tocar a parede no modo realista?**
    O usuário respondeu C, mas explicou que não queria recuo, tela preta nem
    visão do lado de fora. Decisão final registrada: **A — Movimento bloqueado
    exatamente antes da parede, mantendo rotação e recuo disponíveis.**

23. **Qual feedback deve aparecer em uma colisão?**
    Resposta: **C — Flash visual e contador, sem som.**

24. **Como deseja a iluminação interna?**
    Resposta: **B — Interior inteiro claramente iluminado.** Depois foi refinado
    para luz ambiente clara mais luz suave na ponta.

### Rodada 9 — menu, rota e derrota

25. **Como deve ser a tela inicial?**
    Resposta: **A — Dois botões grandes: Modo realista e Exploração livre.**

26. **Como deseja visualizar a rota no nível fácil?**
    Resposta: **A — Linha azul/ciano contínua e luminosa.**

27. **O que acontece ao atingir cinco colisões?**
    Resposta: **A — Movimento congela e aparece “Tentativa encerrada”, com
    Reiniciar e Voltar ao menu.**

### Rodada 10 — tempo, minimapa e vitória

28. **O modo realista terá cronômetro?**
    Resposta: **B — O tempo é registrado, mas aparece apenas no final.**

29. **Como será a câmera do minimapa?**
    Resposta: **B — Vista 3D inclinada e fixa mostrando o rim inteiro.**

30. **O que deve aparecer na tela de vitória?**
    Resposta: **A — Tempo, colisões, Jogar novamente e Menu.**

### Rodada 11 — escala e rotação

31. **Qual escala devemos usar?**
    Resposta original: **B — Rim aumentado.** Implementação consolidada: fonte
    em escala física real e ampliação visual configurada em 5× no Unity.

32. **Como controlar a rotação no modo realista?**
    Resposta: **A — Mouse controla direção/inclinação e Q/E controla rotação
    axial.**

33. **Como controlar a velocidade de avanço?**
    Resposta: **A — Velocidade fixa e lenta.**

### Rodada 12 — criação do rim

34. **Quanto do modelo devemos criar especificamente para o jogo?**
    Resposta: **B — Exterior, pelve, cálices e ureter inteiramente do zero.**

35. **Qual rim será representado?**
    Resposta: **C — Manter o lado indicado pelos dados atuais.** Como os dados
    não confirmam com segurança a lateralidade, o manifesto vigente registra
    “lado a confirmar”.

36. **Em qual região ficará a pedra?**
    Resposta: **B — Cálice médio.**

### Rodada 13 — complexidade e início

37. **Qual complexidade deseja para os cálices?**
    Resposta: **B — Moderada, com três regiões e ramificações menores.**

38. **Onde começa a partida realista?**
    Resposta: **B — Pequeno trecho de ureter antes do rim.**

39. **Quanto aumentaremos a anatomia no jogo?**
    Resposta: **A — Cinco vezes o tamanho real.**

### Rodada 14 — dimensões de jogabilidade

40. **Qual raio terá a ponta virtual?**
    Resposta: **B — 2 mm.**

41. **Qual deverá ser a largura mínima das passagens?**
    Resposta: **B — Aproximadamente 8 mm.**

42. **Como será a pedra?**
    Resposta: **A — Irregular, amarelada e com aproximadamente 6 mm.**

### Rodada 15 — aparência interna

43. **Como devem ser as paredes internas?**
    Resposta: **A — Rosa-avermelhadas, úmidas e com textura discreta.**

44. **Como iluminaremos o interior inteiro sem deixar a imagem plana?**
    Resposta: **A — Luz ambiente clara mais luz suave na ponta.**

45. **Qual campo de visão da câmera interna?**
    Resposta: **B — 80 graus.**

### Rodada 16 — sensação de controle

46. **Qual velocidade fixa de avanço e recuo?**
    Resposta: **B — 20 mm/s na escala física.**

47. **Como funcionará a sensibilidade do mouse?**
    Resposta: **B — Controle deslizante no menu de pausa.**

48. **Como a ponta responderá ao movimento do mouse?**
    Resposta: **B — Suavização moderada.**

### Rodada 17 — exploração, pausa e dados

49. **O que aparece no modo exploração?**
    Resposta: **A — Exterior, sistema coletor, rota e pedra, com controles de
    visibilidade individuais.**

50. **Quais opções devem existir no menu de pausa?**
    Resposta: **A — Continuar, sensibilidade, reiniciar, menu principal e sair.**

51. **Devemos salvar os resultados?**
    Resposta: **A — Não salvar.**

### Rodada 18 — janela, feedback e áudio

52. **Como o jogo deve abrir no Windows?**
    Resposta: **B — Janela redimensionável.**

53. **Como será o flash de colisão?**
    Resposta: **A — Borda vermelha semitransparente por cerca de 0,4 segundo.**

54. **O jogo terá sons?**
    Resposta: **B — Somente interface, vitória e derrota; sem som de colisão e
    sem música.**

### Rodada 19 — idioma, nome e ordem

55. **Qual será o idioma da interface?**
    Resposta: **A — Português.**

56. **Qual nome provisório deseja para o projeto?**
    Resposta: **A — Navegação Renal 3D.**

57. **Em qual ordem você deseja que o projeto seja construído?**
    Resposta: **A — Protótipo funcional simples e depois melhoria visual.** A
    execução foi deliberadamente ajustada depois: o usuário exigiu a correção e
    aprovação do modelo renal antes da fundação definitiva no Unity.

## Decisões vigentes

### Modelo renal

“Rim completo” significa completo para o jogo:

- exterior renal fechado e convincente;
- hilo;
- ureter curto;
- pelve em funil;
- sistema coletor contínuo e navegável;
- grupos superior, médio e inferior;
- nove cálices menores em forma de taça, com impressão papilar côncava;
- rota fixa;
- pedra no cálice médio;
- malhas visual e de colisão separadas.

Não são necessários nesta versão: córtex detalhado, medula, pirâmides, artéria,
veia ou microanatomia. Esses itens não devem ser adicionados silenciosamente.

### Dois rins, ureteres e bexiga

A cena global deverá possuir dois rins, dois ureteres e bexiga. Um rim será o
rim ativo detalhado e navegável. O outro poderá inicialmente reutilizar uma
cópia espelhada/ajustada do exterior aprovado. No modo realista, o jogador
permanece dentro do ureter/sistema coletor do rim ativo. No modo exploração,
deve ser possível sair, observar os dois rins, ureteres e bexiga e entrar/sair
livremente das malhas.

Essa montagem pertence à fundação/ambientação da cena Unity e não altera a
geometria autoritativa do rim ativo.

### Modo realista

- Mouse: direção e inclinação.
- Q/E: rotação axial.
- W/S: avanço e recuo a 20 mm/s físicos.
- Espaço: captura provisória da pedra.
- R: reiniciar.
- P: pausa.
- Esc: libera o cursor.
- SphereCast/subpassos devem impedir atravessamento.
- Ao tocar a parede, parar antes dela; ainda permitir girar e recuar.
- A câmera não pode mostrar a parte externa nem ficar preta.
- Novo episódio de contato conta uma colisão; contato contínuo conta uma vez.
- Cinco colisões encerram a tentativa.
- Rota ciano opcional com T no nível fácil.
- Cronômetro oculto durante a partida e exibido na vitória.

### Exploração livre

- Mouse + WASD para olhar/mover.
- Q/E para descer/subir.
- Shift acelera.
- F recentraliza no conjunto urinário.
- Pode atravessar paredes e entrar/sair dos rins.
- Sem colisão, derrota, tempo ou pontuação.
- Visibilidade individual de exterior, sistema coletor, rota e pedra.

### Hardware futuro

O MPU6050 sozinho não mede avanço/recuo de maneira estável. A arquitetura
futura prevista é:

```text
MPU6050 -> orientação
encoder -> inserção e retirada em milímetros
botão -> comando da garra
ESP32-S3 -> protocolo USB com o Windows
garra física -> captura da pedra real
Meta Quest -> visualização via Link depois do desktop
```

Nenhum hardware deve ser implementado antes da versão com mouse estar
aprovada. A entrada do jogo deve depender de uma abstração como
`IInstrumentInputSource`, permitindo trocar teclado/mouse por ESP32.

## Estado do Marco 1

A primeira entrega `v001` passou em validações técnicas, mas foi rejeitada
visualmente porque o exterior transparente parecia incompleto e o sistema
coletor possuía aparência de tubos com bolas nas pontas.

A correção `v002` foi gerada com:

- exterior opaco no Maya;
- pelve afunilada;
- três grupos principais;
- nove cálices menores côncavos;
- colisão interna com faces voltadas para o lúmen;
- rota e pedra atualizadas;
- versão anterior preservada no histórico Git e no arquivo local do Maya.

Validações finais da `v002`:

- todas as malhas fechadas e manifold;
- rota inteiramente dentro do sistema coletor;
- folga mínima no centro da rota: `3,711 mm`;
- folga exigida para a ponta: `2,5 mm`;
- pedra dentro do cálice-alvo;
- altura exterior no round-trip Maya/FBX: `15,0615 cm`;
- contagens e limites idênticos entre Maya e FBX;
- FBX Maya e FBX Unity com SHA-256 idêntico:
  `174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c`.

## Arquivos autoritativos locais

Maya editável:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Source\Kidney_Master.ma`

FBX exportado pelo Maya:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Exports\Kidney_Game_v002.fbx`

Cópia validada para Unity:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Unity\Assets\Art\Kidney\Models\Kidney_Game_v002.fbx`

Relatórios e prévias:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Documentation`

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01\Previews`

## Marcos vigentes

### Marco 1 — modelo renal

Estado: `v002` gerada e validada tecnicamente. A aprovação visual do usuário é
o aceite final. O modelo não deve ser chamado de clinicamente validado.

### Marco 2 — fundação Unity e importação

Estado: implementado e validado em Unity `6000.5.0f1`.

- projeto Unity 6 URP novo criado, sem reutilizar o protótipo antigo;
- `Kidney_Game_v002.fbx` importado com altura física de `0,150614 m` e raiz
  visual `5×`;
- prefab `KidneyLevel`, layers, materiais, cenas e gerador reproduzível criados;
- exterior, interior, collider, rota, pedra e âncoras reconhecidos e validados;
- base visual com dois rins, dois ureteres e bexiga montada;
- modos Realista e Exploração receberam controles provisórios para permitir o
  primeiro teste com mouse, sem declarar a navegação final concluída;
- relatório automático aprovado sem erros e prévias registradas em
  `Unity/Documentation`.

### Marco 3 — navegação realista

Estado: implementado e validado automaticamente no Unity `6000.5.0f1`.

- `MouseEndoscopeController` preservado para manter as referências da cena,
  porém sem dependência de `CharacterController`;
- ponta com raio de `0,010 m`, velocidade de `0,10 m/s`, subpassos máximos de
  `0,005 m` e margem de `0,001 m`;
- `SphereCast` restrito à camada `KidneyCollision`, com faces internas ativas;
- bloqueio total do avanço na parede, mantendo rotação e recuo;
- um toque por contato contínuo, rearmado a `0,015 m` sem parede;
- mouse amortecido, limite de `70°/s`, suavização de `0,12 s` e Q/E a `55°/s`;
- clique prende o cursor; Esc, pausa e Exploração liberam;
- reset direto no `StartAnchor`, sem alternar componentes físicos;
- equivalência de movimento validada automaticamente em 30, 60 e 120 FPS;
- câmera interna FOV `80`, luz, rota T, minimapa M, captura Espaço e F1/F2
  preservados;
- FBX `Kidney_Game_v002` permanece byte a byte inalterado.
- acabamento visual pendente concluído sem alterar o rim superior esquerdo:
  rim direito fechado e vermelho, ureteres curvos suavizados, bexiga orgânica
  com saída inferior e remoção dos materiais cinza provisórios;
- textura interna original gerada para o projeto, importada como base color e
  normal map em `Assets/Art/Textures/Organic`;
- capturas automáticas externa e internas registradas em
  `Unity/Documentation/Previews`;
- relatório `marco3_validation.json` aprovado sem erros.

### Marco 3.1 — integração visual Meshy

Estado: implementado e validado automaticamente no Unity `6000.5.0f1`.

- a malha navegável `Kidney_Game_v002`, o collider interno, a rota, a pedra e
  as âncoras não foram refeitos nem alterados;
- a fonte Meshy de alta resolução possui `15.979` triângulos e `46,5 cm`;
- o pipeline Maya corrige componentes non-manifold, transfere UVs do pacote
  texturizado anterior e separa as partes anatômicas;
- a conversão de lateralidade Maya/Unity foi tratada no exportador: o rim
  externo fechado correto é o rim direito enviado ao jogo;
- a cena Maya editável é
  `Candidates/Meshy_Urinary_System_v002/Source/Meshy_Urinary_System_v002.ma`;
- o FBX visual contém `Meshy_RightKidney` e
  `Meshy_UretersAndBladder`, totalizando `11.026` triângulos e `7.931` UVs;
- o rim esquerdo Meshy e a malha doadora permanecem ocultos em
  `REFERENCE_ONLY` e não entram no FBX do Unity;
- Base Color, Normal, Metallic e Roughness foram preservados; um Mask Map URP
  foi gerado com Metallic em R e `1 - Roughness` em A;
- o novo conjunto usa escala uniforme `5×`, camada `KidneyExterior`, material
  URP compartilhado e nenhum collider;
- o rim ativo aprovado foi preservado; após a primeira captura, ele recebeu um
  ajuste isolado de `0,075 m` para a direita na escala visual (`0,015 m`
  físicos), conectando sua saída ao ureter esquerdo sem mover o sistema Meshy;
- os assets procedurais anteriores continuam no repositório como fallback,
  mas não são instanciados na cena;
- `58` verificações passaram, incluindo colisão, contatos, reset, modos,
  cursor e equivalência em 30/60/120 FPS;
- relatório `marco31_validation.json` e três capturas automáticas foram
  registrados sem controlar a janela do usuário;
- SHA-256 do interior v002 preservado:
  `174fabbf6ec31b3052360be995b5bbc4fb7e074b91ef2a2bba838ca45cc0fa9c`;
- SHA-256 do visual Meshy sincronizado Maya/Unity:
  `f8408f41656011f65cb737b1b434e7611228036b76fb8fcadfaa183dcfa26ed2`.

### Marco 3.2 — junção rim/ureter v003

Estado: implementado e validado automaticamente no Unity `6000.5.0f1`.

- o deslocamento provisório de `0,075 m` do rim ativo foi removido;
- o rim voltou à posição original `(-0,44; 0,34; 0)` no mundo Unity;
- a v003 reconstrói apenas a transição proximal entre pelve e ureter Meshy;
- o ureter Meshy completo permanece visual e sem collider;
- rota, collider, StartAnchor e gola visual seguem o mesmo novo centro;
- a medição fixa entre a interface v003 e o topo do ureter é `0,000041 m`;
- a cena Maya completa é
  `Candidates/Kidney_Game_v003/Source/Urinary_System_Assembly_v003.ma`;
- a v002 permaneceu byte a byte inalterada;
- SHA-256 da v003:
  `f721e63ad9188f007520709f24cb7c60e85ce3a2588bec6b533e7460fddc9bcf`;
- relatório `marco32_validation.json` e quatro capturas foram registrados;
- o Marco 4 continua não iniciado.

### Marco 4 — gameplay

Estados da partida, cinco colisões, flash vermelho, rota, captura, vitória,
derrota, pausa e reinício.

### Marco 5 — exploração e minimapa

Câmera livre, entrada/saída das malhas, controles de visibilidade, conjunto com
dois rins/ureteres/bexiga e minimapa inclinado.

### Marco 6 — polimento e integração futura

Materiais, interface, áudio, testes, build Windows e, somente depois da versão
desktop aprovada, planejamento/implementação de ESP32, MPU, encoder, garra e
Quest.

## Restrições importantes

- Não reutilizar o Unity antigo como base do novo jogo.
- Não alterar o código científico antigo sem necessidade explícita.
- Não executar A* durante o jogo.
- Não usar NavMesh ou controlador FPS no modo realista.
- Não criar múltiplos pacientes/casos nesta primeira versão.
- Não permitir divergência entre a geometria Maya e Unity.
- Não avançar um marco sem validação proporcional ao risco e registro do
  resultado.
- Preservar o trabalho antigo no histórico Git; não apagar versões de maneira
  irrecuperável.

## Próxima ação

Planejar e implementar o gameplay final do Marco 4. Após o Marco 4, sincronizar
no Maya o sistema completo com dois rins, ureteres e bexiga antes do Marco 5.
ESP32, MPU, encoder e garra continuam fora desta etapa.
