# Instruções para agentes

## Fonte ativa

- A branch de integração é `DoZero`.
- O projeto ativo é `Navegacao_Renal_3D`; abra o Unity em
  `Navegacao_Renal_3D/Unity` com a versão `6000.5.0f1`.
- Leia `Navegacao_Renal_3D/PROJECT_STATUS.md` antes de planejar um novo marco.
- O pipeline Python da raiz e `UnityVRPrototype` são legado preservado. Não os
  altere ou reutilize como base sem pedido explícito do usuário.

## Antes de editar

- Execute `git status --short --branch` e preserve qualquer trabalho local
  preexistente.
- Não apague versões antigas nem reescreva o histórico Git.
- Mantenha mudanças funcionais, automação e documentação em commits separados.
- Não normalize finais de linha em massa.

## Invariantes técnicos

- Preserve a correspondência entre os artefatos Maya e Unity e os hashes
  registrados em `PROJECT_STATUS.md`.
- Não execute A* em runtime, não use NavMesh no modo Realista e não substitua o
  controlador por um FPS genérico.
- A captura usa a âncora entre as mandíbulas a no máximo `0,018 m` e exige
  caminho livre.
- O protocolo do controlador permanece JSON v2; não declare validação física
  enquanto o conjunto real não tiver sido testado.
- Quest, encoder, servo e sensor físico estão fora do escopo atual.

## Validação e Git

- Durante mudanças, use `.\scripts\validate-dozero.ps1`.
- Antes de integrar ou publicar, faça commit e execute
  `.\scripts\validate-dozero.ps1 -RequireClean`.
- Novos OBJ/PNG do projeto ativo devem usar Git LFS. Não migre os binários
  existentes nem o histórico sem autorização explícita.
- Não use force-push na `DoZero`. Se o remoto divergir, faça integração
  preservando o histórico e pare para revisão caso existam conflitos.
