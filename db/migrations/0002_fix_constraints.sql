-- Regras do banco que contradiziam o app. Pode ser rodado mais de uma vez.
BEGIN;

-- Contas criadas pelo Google não têm senha.
ALTER TABLE account ALTER COLUMN password DROP NOT NULL;

-- A subcategoria é de uma conta e de uma categoria; o nome não é único no banco inteiro.
ALTER TABLE subcategory DROP CONSTRAINT IF EXISTS constraint_name;
CREATE UNIQUE INDEX IF NOT EXISTS uq_subcategory_account_category_name ON subcategory (accountid, categoryid, name);

-- Ações têm uma posição por código; cada aplicação de renda fixa é um registro.
ALTER TABLE stock DROP CONSTRAINT IF EXISTS uq_stock_account_ticker;
CREATE UNIQUE INDEX IF NOT EXISTS uix_stock_account_ticker_acao ON stock (accountid, ticker) WHERE isstock;

COMMIT;
