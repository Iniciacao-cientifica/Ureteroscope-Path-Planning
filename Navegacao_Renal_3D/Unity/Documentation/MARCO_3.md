# Navegação Renal 3D — Marco 3

## Resultado

O modo Realista agora usa um controlador próprio por `SphereCast`. O
`CharacterController` provisório foi removido e o rim ativo `v002` não foi
alterado.

O acabamento visual pendente também foi concluído. O rim ativo superior
esquerdo conserva o material aprovado; o rim direito agora é fechado, opaco,
vermelho e orgânico. Os dois ureteres foram substituídos por tubos curvos de
alta densidade, a bexiga recebeu formato orgânico com saída inferior e todo o
conjunto deixou de usar o acabamento cinza provisório.

Parâmetros da navegação:

- ponta esférica: `0,010 m` no mundo Unity, equivalente a `2 mm` físicos na
  apresentação `5×`;
- velocidade: `0,10 m/s` no Unity, equivalente a `20 mm/s` físicos;
- movimento dividido em subpassos de no máximo `0,005 m`;
- margem antes da parede: `0,001 m`;
- colisão consultada somente na camada `KidneyCollision`;
- faces internas habilitadas nas consultas físicas;
- contato contínuo conta uma vez e rearma sem parede a menos de `0,015 m`;
- direção limitada a `70°/s`, com suavização de `0,12 s`;
- rolamento `Q/E` a `55°/s`.

Ao encontrar a parede, a ponta interrompe o avanço, mas continua podendo girar
e recuar. A câmera interna mantém FOV `80`, luz própria e não renderiza o
exterior do rim.

## Como abrir

No Unity Hub, adicione e abra:

`C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Unity`

Use Unity `6000.5.0f1`. Abra `Assets/Scenes/MainMenu.unity` para começar pelo
menu ou `Assets/Scenes/KidneyGame.unity` para ir direto ao jogo.

## Controles

### Realista

- clique esquerdo dentro do Game View: prender o cursor;
- `Esc`: liberar o cursor;
- mouse: orientar a ponta;
- `W`/seta para cima: avançar;
- `S`/seta para baixo: recuar;
- `Q`/`E`: rotação axial;
- `Espaço`: capturar a pedra quando estiver perto;
- `P`: pausar;
- `R`: reiniciar no `StartAnchor`;
- `T`: mostrar/ocultar a rota;
- `M`: mostrar/ocultar o minimapa;
- `F2`: Exploração.

### Exploração

- botão direito + mouse: olhar;
- `WASD` e `Q/E`: mover livremente;
- `Shift`: acelerar;
- `F1`: voltar ao modo Realista.

Pausa e modo Exploração sempre liberam o cursor.

## Validação automática

O comando `Navegacao Renal > Construir Marco 3` recria a cena atual, valida o
controlador e gera as capturas. O relatório fica em
`Documentation/marco3_validation.json`.

São verificados automaticamente configuração física, ausência do
`CharacterController`, scripts ausentes, bloqueio de um deslocamento grande,
equivalência em 30/60/120 FPS, avanço/recuo, latch e rearme de contato, reset,
pausa, dois modos, câmera e integridade SHA-256 do FBX v002.

As imagens internas com a rota desligada e ligada ficam em
`Documentation/Previews/marco3_realistic_route_off.png` e
`Documentation/Previews/marco3_realistic_route_on.png`.

A composição externa validada fica em
`Documentation/Previews/marco3_visual_system.png`. A validação também confirma
automaticamente o material próprio e opaco do rim direito, a textura interna,
a densidade das malhas dos ureteres e da bexiga e a presença da saída inferior.

## Textura orgânica original

A textura de mucosa foi criada especificamente para este projeto pelo gerador
de imagens integrado do Codex e não copia a fotografia de referência. Os
arquivos Unity são:

- `Assets/Art/Textures/Organic/T_RenalMucosa_BaseColor_v001.png`;
- `Assets/Art/Textures/Organic/T_RenalMucosa_NormalSource_v001.png`.

O segundo arquivo é importado pelo Unity como normal map derivado. Prompt
registrado para reprodução:

> Use case: scientific-educational. Asset type: seamless PBR base-color texture
> for the interior wall of a renal collecting system in a Unity URP medical
> training game. Primary request: create an original seamless tissue surface
> texture inspired by healthy moist urothelial mucosa, not a copied photograph.
> Scene/backdrop: flat orthographic macro surface only, no tunnel, no cavity,
> no horizon, no object framing. Style/medium: highly realistic medical 3D
> material scan appearance. Color palette: natural deep pink, muted red and
> subtle burgundy variation; avoid neon magenta. Materials/textures: fine
> irregular microfolds, very shallow soft ridges, subtle vascular mottling, wet
> glossy highlights distributed sparsely, organic but not grotesque.
> Composition/framing: uniform detail density across the full square; edges
> must tile seamlessly in every direction; no dominant center. Constraints: no
> blood, no wounds, no lesions, no stones, no text, no watermark, no black
> background, no directional lighting baked strongly into the texture;
> suitable for deriving normal and roughness maps.

## Limites mantidos

- somente o rim ativo é navegável;
- segundo rim é visual;
- um único caminho e nível fácil;
- sem A* durante a partida;
- sem ESP32, MPU, encoder ou garra neste marco;
- a sincronização Maya do sistema urinário completo ocorrerá depois do Marco 4.

O Marco 4 não faz parte desta entrega e não foi iniciado automaticamente.
