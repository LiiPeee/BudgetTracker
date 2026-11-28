-- Estrutura de produção como estava em 27/11/2026, antes de 0002.
-- Só estrutura. NÃO rodar no banco de produção: ele já está assim.
-- Os registros de recurrence, typecontact, typetransaction e category ainda não estão versionados.

CREATE TABLE account (
    id                           bigserial PRIMARY KEY,
    firstname                    varchar(50)   NOT NULL,
    lastname                     varchar(50)   NOT NULL,
    email                        varchar(64)   NOT NULL,
    password                     varchar(255)  NOT NULL,
    balance                      numeric(18,2) NOT NULL DEFAULT 0,
    role                         varchar(20)   NOT NULL DEFAULT 'User',
    refreshtoken                 varchar(500),
    refreshtokenexpirytime       timestamptz,
    emailverified                boolean       NOT NULL DEFAULT false,
    verifiedat                   timestamptz,
    emailverificationtoken       varchar(255),
    emailverificationtokenexpiry timestamptz,
    verifyattempts               bigint        NOT NULL DEFAULT 0,
    isactive                     boolean       NOT NULL DEFAULT true,
    createdat                    timestamptz   NOT NULL DEFAULT now(),
    updatedat                    timestamptz,
    loginattempts                bigint        NOT NULL DEFAULT 0,
    CONSTRAINT uq_account_email UNIQUE (email)
);
CREATE INDEX idx_account_isactive ON account (isactive);
CREATE INDEX idx_account_refreshtoken ON account (refreshtoken) WHERE refreshtoken IS NOT NULL;
CREATE UNIQUE INDEX uix_account_email ON account (email);

CREATE TABLE category (
    id          bigserial PRIMARY KEY,
    name        varchar(100) NOT NULL,
    description varchar(255),
    isactive    boolean      NOT NULL DEFAULT true,
    createdat   timestamptz  NOT NULL DEFAULT now(),
    updatedat   timestamptz
);

CREATE TABLE recurrence (
    id        bigint PRIMARY KEY,
    name      varchar(50) NOT NULL,
    createdat timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE typecontact (
    id        bigint PRIMARY KEY,
    name      varchar(50) NOT NULL,
    createdat timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE typetransaction (
    id        bigint PRIMARY KEY,
    name      varchar(50) NOT NULL,
    createdat timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE contact (
    id            bigserial PRIMARY KEY,
    name          varchar(100) NOT NULL,
    email         varchar(255),
    phone         varchar(30),
    document      varchar(50),
    isactive      boolean      NOT NULL DEFAULT true,
    accountid     bigint       NOT NULL,
    typecontactid bigint       NOT NULL,
    createdat     timestamptz  NOT NULL DEFAULT now(),
    updatedat     timestamptz,
    CONSTRAINT fk_contact_account FOREIGN KEY (accountid) REFERENCES account (id) ON DELETE CASCADE,
    CONSTRAINT fk_contact_typecontact FOREIGN KEY (typecontactid) REFERENCES typecontact (id)
);
CREATE INDEX idx_contact_accountid ON contact (accountid);
CREATE INDEX idx_contact_typecontactid ON contact (typecontactid);

CREATE TABLE address (
    id        bigserial PRIMARY KEY,
    street    varchar(255) NOT NULL,
    city      varchar(100) NOT NULL,
    state     varchar(100) NOT NULL,
    zipcode   varchar(20)  NOT NULL,
    country   varchar(100) NOT NULL,
    isprimary boolean      NOT NULL DEFAULT false,
    contactid bigint       NOT NULL,
    createdat timestamptz  NOT NULL DEFAULT now(),
    updatedat timestamptz,
    accountid bigint,
    CONSTRAINT fk_address_contact FOREIGN KEY (contactid) REFERENCES contact (id) ON DELETE CASCADE
);
CREATE INDEX idx_address_contactid ON address (contactid);

CREATE TABLE budgetlimit (
    id          bigserial PRIMARY KEY,
    islimit     boolean       NOT NULL DEFAULT true,
    month       integer       NOT NULL,
    year        integer       NOT NULL,
    limitamount numeric(18,2) NOT NULL,
    percentage  numeric(5,2)  NOT NULL DEFAULT 0,
    categoryid  bigint        NOT NULL,
    accountid   bigint        NOT NULL,
    createdat   timestamptz   NOT NULL DEFAULT now(),
    updatedat   timestamptz,
    CONSTRAINT fk_budgetlimit_account FOREIGN KEY (accountid) REFERENCES account (id) ON DELETE CASCADE,
    CONSTRAINT fk_budgetlimit_category FOREIGN KEY (categoryid) REFERENCES category (id) ON DELETE CASCADE,
    CONSTRAINT uq_budgetlimit_account_category_month_year UNIQUE (accountid, categoryid, month, year)
);

CREATE TABLE resetpassword (
    id          bigserial PRIMARY KEY,
    accountid   bigint       NOT NULL,
    hashedtoken varchar(500) NOT NULL,
    expireat    timestamptz  NOT NULL,
    createdat   timestamptz  NOT NULL DEFAULT now(),
    updatedat   timestamptz,
    CONSTRAINT fk_resetpassword_account FOREIGN KEY (accountid) REFERENCES account (id) ON DELETE CASCADE
);
CREATE INDEX idx_resetpassword_accountid ON resetpassword (accountid);
CREATE INDEX idx_resetpassword_hashedtoken ON resetpassword (hashedtoken);

CREATE TABLE stock (
    id              bigserial PRIMARY KEY,
    accountid       bigint        NOT NULL,
    ticker          varchar(20)   NOT NULL,
    title           varchar(100)  NOT NULL,
    description     varchar(255),
    pricebuyed      numeric(18,2) NOT NULL,
    pricemarket     numeric(18,2) NOT NULL DEFAULT 0,
    avarage         varchar(30)   NOT NULL DEFAULT 0,
    quantity        numeric(18,4) NOT NULL DEFAULT 0,
    createdat       timestamptz   NOT NULL DEFAULT now(),
    updatedat       timestamptz,
    isstock         boolean       NOT NULL DEFAULT true,
    cdirate         numeric(18,2),
    investmentdate  date,
    fixedincometype varchar(50),
    CONSTRAINT fk_stock_account FOREIGN KEY (accountid) REFERENCES account (id) ON DELETE CASCADE,
    CONSTRAINT uq_stock_account_ticker UNIQUE (accountid, ticker)
);

CREATE TABLE subcategory (
    id          bigserial PRIMARY KEY,
    name        varchar(100) NOT NULL,
    description varchar(255),
    isactive    boolean      NOT NULL DEFAULT true,
    categoryid  bigint,
    accountid   bigint       NOT NULL,
    createdat   timestamptz  NOT NULL DEFAULT now(),
    updatedat   timestamptz,
    CONSTRAINT constraint_name UNIQUE (name),
    CONSTRAINT fk_subcategory_account FOREIGN KEY (accountid) REFERENCES account (id) ON DELETE CASCADE,
    CONSTRAINT fk_subcategory_category FOREIGN KEY (categoryid) REFERENCES category (id) ON DELETE SET NULL
);
CREATE INDEX idx_subcategory_accountid ON subcategory (accountid);
CREATE INDEX idx_subcategory_categoryid ON subcategory (categoryid) WHERE categoryid IS NOT NULL;

CREATE TABLE transactions (
    id                  bigserial PRIMARY KEY,
    amount              numeric(18,2) NOT NULL,
    name                varchar(100)  NOT NULL,
    description         varchar(255),
    paid                boolean       NOT NULL DEFAULT false,
    numberofinstallment bigint,
    dateofinstallment   date,
    quantityinstallment varchar(50),
    recurrenceid        bigint        NOT NULL,
    contactid           bigint,
    subcategoryid       bigint,
    accountid           bigint        NOT NULL,
    categoryid          bigint        NOT NULL,
    typetransactionid   bigint        NOT NULL,
    createdat           timestamptz   NOT NULL DEFAULT now(),
    updatedat           timestamptz,
    competencedate      date          NOT NULL,
    CONSTRAINT chk_transactions_amount CHECK (amount > 0),
    CONSTRAINT fk_transactions_account FOREIGN KEY (accountid) REFERENCES account (id) ON DELETE CASCADE,
    CONSTRAINT fk_transactions_category FOREIGN KEY (categoryid) REFERENCES category (id),
    CONSTRAINT fk_transactions_contact FOREIGN KEY (contactid) REFERENCES contact (id) ON DELETE SET NULL,
    CONSTRAINT fk_transactions_recurrence FOREIGN KEY (recurrenceid) REFERENCES recurrence (id),
    CONSTRAINT fk_transactions_subcategory FOREIGN KEY (subcategoryid) REFERENCES subcategory (id) ON DELETE SET NULL,
    CONSTRAINT fk_transactions_type FOREIGN KEY (typetransactionid) REFERENCES typetransaction (id)
);
CREATE INDEX idx_transactions_account_category ON transactions (accountid, categoryid);
CREATE INDEX idx_transactions_account_createdat ON transactions (accountid, createdat DESC);
CREATE INDEX idx_transactions_accountid ON transactions (accountid);
CREATE INDEX idx_transactions_categoryid ON transactions (categoryid);
CREATE INDEX idx_transactions_contactid ON transactions (contactid) WHERE contactid IS NOT NULL;
CREATE INDEX idx_transactions_recurrenceid ON transactions (recurrenceid);
CREATE INDEX idx_transactions_subcategoryid ON transactions (subcategoryid) WHERE subcategoryid IS NOT NULL;
CREATE INDEX idx_transactions_typetransactionid ON transactions (typetransactionid);
