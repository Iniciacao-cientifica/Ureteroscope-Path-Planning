# Fluxo de desenvolvimento da `DoZero`

`DoZero` é a base estável de integração do projeto ativo. Novos marcos devem
ser implementados em branches curtas e integrados somente após validação.

## Início de um marco

```powershell
git switch DoZero
git fetch origin
git pull --ff-only origin DoZero
git switch -c feat/marco-X-descricao
```

Antes de editar, verifique `git status` e leia
`Navegacao_Renal_3D/PROJECT_STATUS.md`. Alterações preexistentes devem ser
preservadas e entendidas antes de continuar.

## Commits

- `feat(marco-X): ...` para novas capacidades;
- `fix(marco-X): ...` para correções funcionais;
- `build: ...` para validação e automação;
- `docs: ...` para documentação e governança;
- não misturar mudanças funcionais, arquivos gerados e documentação sem
  necessidade técnica.

Não usar force-push na `DoZero`, não fazer squash de marcos aprovados e não
reescrever o histórico compartilhado.

## Validação

Durante o desenvolvimento:

```powershell
.\scripts\validate-dozero.ps1
```

Após o commit e antes da integração ou push:

```powershell
.\scripts\validate-dozero.ps1 -RequireClean
```

O relatório produzido por esse comando é transitório e permanece em
`Navegacao_Renal_3D/Unity/Logs/`. Para consolidar oficialmente um marco, use o
menu correspondente do Unity e versione o relatório canônico em
`Navegacao_Renal_3D/Unity/Documentation/` junto com a documentação atualizada.

## Git LFS

Não reconverta OBJ/PNG antigos nem migre o histórico. Antes de adicionar um
novo OBJ ou PNG ao projeto ativo, registre seu caminho ou padrão específico:

```powershell
git lfs track "Navegacao_Renal_3D/Maya/Candidates/Nova_Versao/**/*.obj"
git lfs track "Navegacao_Renal_3D/Maya/Candidates/Nova_Versao/**/*.png"
git add .gitattributes
```

O validador rejeita novos OBJ/PNG ativos adicionados ao índice sem atributo
LFS. Arquivos existentes permanecem como estão.

## Integração

1. Execute o gate com `-RequireClean`.
2. Atualize as referências com `git fetch origin`.
3. Confirme que a integração não exige force-push.
4. Integre a branch curta na `DoZero`, valide novamente e faça push normal.
5. Se o remoto tiver divergido ou houver conflitos, preserve ambos os lados e
   revise a integração antes de publicar.
