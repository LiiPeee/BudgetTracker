# Banco de dados

Scripts em `migrations/`, aplicados em ordem numérica.

| Script | O que é |
|---|---|
| `0001_baseline.sql` | Estrutura que produção já tinha em 27/11/2026. **Não rodar em produção.** |
| `0002_fix_constraints.sql` | Correções de regras. Rodar em produção, depois de um backup. |

Para aplicar em produção (a senha é pedida na hora):

```powershell
psql -h <host> -U <usuario> -d <banco> -v ON_ERROR_STOP=1 -f db/migrations/0002_fix_constraints.sql
```

Em um banco novo, rode todos os scripts em ordem. Os registros de `recurrence`, `typecontact`,
`typetransaction` e `category` ainda não estão versionados.

A aplicação ainda é manual; a API não altera o banco sozinha. Toda mudança de estrutura entra como um
novo script numerado, e os testes de integração (`ProductionSchemaTest`) rodam sobre eles.
