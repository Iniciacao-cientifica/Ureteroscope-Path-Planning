# Ureteroscope Path Planning

Repositório acadêmico para pesquisa e treinamento em navegação
ureteroscópica. A linha ativa de desenvolvimento está na branch `DoZero` e usa
como fonte principal o projeto [`Navegacao_Renal_3D`](Navegacao_Renal_3D/README.md).

> Protótipo de pesquisa e treinamento. Não é validado para diagnóstico,
> atendimento a pacientes, navegação intraoperatória ou controle robótico.

## Mapa do repositório

| Área | Estado | Finalidade |
| --- | --- | --- |
| `Navegacao_Renal_3D/` | **Ativa** | Unity 6, Maya, modelos, validações e ferramentas do jogo atual. |
| `hardware/firmware/ureteroscope_controller/` | **Ativa** | Firmware ESP32 DevKit V1, MPU6050 e botão físico. |
| `scripts/` | **Ativa** | Validação integrada da branch `DoZero`. |
| Pipeline Python na raiz | Legado preservado | Planejamento A*, exportação de casos e integração com o protótipo VR anterior. |
| `UnityVRPrototype/` | Legado preservado | Projeto Unity anterior para Quest e treinamento desktop. |

O legado continua funcional e não deve ser usado como base do novo jogo. Suas
instruções originais estão em [Pipeline legado](docs/LEGACY_PIPELINE.md).

## Estado atual

- Marcos 1–6 implementados;
- Marco 6 aprovado com `194` verificações (`133` herdadas + `61` próprias);
- controle ESP32/MPU validado por simulação e replay;
- teste elétrico com o hardware físico ainda pendente;
- próxima evolução aprovada para planejamento: Marco 5.1, com revisão
  anatômica v004 antes de qualquer substituição no Unity.

O estado canônico, hashes protegidos e restrições estão em
[PROJECT_STATUS.md](Navegacao_Renal_3D/PROJECT_STATUS.md).

## Validação da `DoZero`

No PowerShell, a partir da raiz:

```powershell
.\scripts\validate-dozero.ps1
```

Antes de integrar ou publicar um marco, use o gate de worktree limpo:

```powershell
.\scripts\validate-dozero.ps1 -RequireClean
```

O comando verifica Git, LFS, testes Python, firmware `esp32dev` e Unity
`6000.5.0f1`. O fluxo de branches, commits, relatórios e novos arquivos LFS
está em [DEVELOPMENT_WORKFLOW.md](docs/DEVELOPMENT_WORKFLOW.md).

## Abertura do projeto ativo

No Unity Hub, adicione a pasta:

```text
Navegacao_Renal_3D/Unity
```

Use Unity `6000.5.0f1` e abra `Assets/Scenes/MainMenu.unity`.
