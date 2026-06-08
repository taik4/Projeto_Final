-- ============================================================
-- LiceBank Banking Core - Script de Inicialização do MySQL 8.0
-- ============================================================
-- Executado automaticamente pelo Docker no primeiro boot do container.
-- Contém: Schema, Stored Procedures, Views, Functions e Triggers.
-- ============================================================

-- ------------------------------------------------------------
-- 1. Criação do Database
-- ------------------------------------------------------------
CREATE DATABASE IF NOT EXISTS banking_core
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE banking_core;

-- ------------------------------------------------------------
-- 1.1. Criação explícita do usuário da aplicação
-- ------------------------------------------------------------
-- Garante que o usuário licebank_app exista mesmo que o volume Docker
-- tenha sido restaurado de backup ou reinicializado com credenciais
-- divergentes. Redundante com as variáveis MYSQL_USER/MYSQL_PASSWORD
-- do docker-compose, mas defensivo contra inconsistências.
CREATE USER IF NOT EXISTS 'licebank_app'@'%' IDENTIFIED BY 'LiceBank@App2024';
GRANT ALL PRIVILEGES ON banking_core.* TO 'licebank_app'@'%';
FLUSH PRIVILEGES;

-- ============================================================
-- 2. Função de Mascaramento de Documento
-- ============================================================
-- Mascarar CPF (11 dígitos): exibe apenas os 3 primeiros e 2 últimos.
-- Exemplo: 12345678901 -> 123***8901  (na prática: 123.***.**-01)
-- Usada nativamente em Views para que a API nunca receba o dado pleno.
-- (CONSTITUTION.md — Lei I.3: Mascaramento na Origem)

DELIMITER $$

DROP FUNCTION IF EXISTS fn_mask_document$$
CREATE FUNCTION fn_mask_document(doc VARCHAR(14))
RETURNS VARCHAR(14)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE masked VARCHAR(14);
    IF doc IS NULL OR CHAR_LENGTH(doc) < 5 THEN
        RETURN '***';
    END IF;
    -- CPF (11 dígitos): mostra 3 primeiros + *** + 2 últimos
    IF CHAR_LENGTH(doc) = 11 THEN
        SET masked = CONCAT(LEFT(doc, 3), '.***.***-', RIGHT(doc, 2));
    -- CNPJ (14 dígitos): mostra 2 primeiros + *** + 4 últimos
    ELSEIF CHAR_LENGTH(doc) = 14 THEN
        SET masked = CONCAT(LEFT(doc, 2), '.***.***/***-', RIGHT(doc, 4));
    ELSE
        SET masked = CONCAT(LEFT(doc, 2), '***', RIGHT(doc, 2));
    END IF;
    RETURN masked;
END$$

DELIMITER ;

-- ============================================================
-- 3. Tabela: accounts
-- ============================================================
-- Design decisions:
--   • account_id: BINARY(16) armazena UUID de forma compacta (vs CHAR(36)).
--     A aplicação (.NET) converte Guid <-> byte[16] na camada de infra.
--   • holder_cpf_hash: VARBINARY(64) armazena SHA-256 do CPF.
--     Nunca armazenamos o CPF em texto plano (CONSTITUTION — Lei I.2).
--   • balance: DECIMAL(15,2) com CHECK >= 0 garante RN03 (sem saldo negativo).
--   • status: ENUM controla ciclo de vida da conta sem flags booleanas.

CREATE TABLE IF NOT EXISTS accounts (
    account_id        CHAR(36)        NOT NULL    COMMENT 'UUID v4 (CHAR(36) — padrão Pomelo/EF Core)',
    user_id           CHAR(36)        NULL        COMMENT 'FK para tabela users — dono da conta (FASE 3)',
    holder_name       VARCHAR(120)    NOT NULL    COMMENT 'Nome completo do titular',
    holder_email      VARCHAR(255)    NOT NULL    COMMENT 'Email do titular',
    holder_cpf_hash   VARBINARY(64)   NOT NULL    COMMENT 'SHA-256 do CPF (nunca armazena CPF pleno)',
    balance           DECIMAL(15,2)   NOT NULL DEFAULT 0.00 COMMENT 'Saldo disponível em BRL',
    status            ENUM('ACTIVE', 'BLOCKED', 'CLOSED')
                                      NOT NULL DEFAULT 'ACTIVE' COMMENT 'Status da conta',
    created_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    deleted_at        DATETIME(3)     NULL        COMMENT 'Soft delete — NULL se ativa',

    CONSTRAINT pk_accounts PRIMARY KEY (account_id),
    CONSTRAINT uq_accounts_email UNIQUE (holder_email),
    CONSTRAINT uq_accounts_user UNIQUE (user_id),
    CONSTRAINT chk_balance_positive CHECK (balance >= 0.00)
) ENGINE=InnoDB
  COMMENT='Contas bancárias do sistema';

-- Índice para busca por hash de CPF (login via CPF)
CREATE INDEX ix_accounts_cpf_hash ON accounts (holder_cpf_hash);

-- ============================================================
-- 3.5. Tabela: users (FASE 2 — Auth)
-- ============================================================
-- Separada da tabela accounts para isolar credenciais de dados financeiros.
-- Design decisions:
--   • id: UUID como CHAR(36) para facilitar debug em desenvolvimento.
--     A aplicação .NET usa Guid e converte.
--   • email: único para login (RF01).
--   • cpf_hash: CHAR(64) armazena hash SHA-256 em formato hex (64 chars).
--     Nunca armazena CPF pleno (CONSTITUTION — Lei I.2).
--   • password_hash: VARCHAR(255) para acomodar BCrypt ($2a$12$... ~60 chars).
--   • account_id: FK opcional para a conta bancária principal do usuário.

CREATE TABLE IF NOT EXISTS users (
    id         CHAR(36)    NOT NULL COMMENT 'UUID em formato texto (CHAR(36) para compatibilidade com EF Core + Pomelo)',
    email      VARCHAR(255) NOT NULL COMMENT 'Email único para login (RF01)',
    cpf_hash   VARCHAR(64) NOT NULL COMMENT 'SHA-256 do CPF em hex (64 chars)',
    password_hash VARCHAR(255) NOT NULL COMMENT 'BCrypt hash da senha (work factor 12)',
    account_id CHAR(36) NULL COMMENT 'FK opcional para a conta principal do usuário',
    created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),

    CONSTRAINT pk_users PRIMARY KEY (id),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT uq_users_cpf_hash UNIQUE (cpf_hash),
    CONSTRAINT fk_users_account FOREIGN KEY (account_id)
        REFERENCES accounts (account_id) ON DELETE SET NULL
) ENGINE=InnoDB
  COMMENT='Usuários do sistema (credenciais e auth — Fase 2)';
-- ============================================================
-- 4. Tabela: transactions
-- ============================================================
-- Design decisions:
--   • end_to_end_id: CHAR(32) identifica univocamente cada PIX (padrão BACEN).
--     Formato: [ISPB 8 chars] + [Data YYYYMMDD] + [Sequencial 16 chars].
--   • idempotency_key: BINARY(16) — UUID enviado pelo client.
--     Garante RN02: a SP rejeita duplicatas (CONSTITUTION — Lei II.2).
--   • amount: DECIMAL(15,2) para precisão monetária.
--   • direction: ENUM('DEBIT', 'CREDIT') define se a transação saiu ou entrou.

CREATE TABLE IF NOT EXISTS transactions (
    transaction_id    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'PK sequencial (para keyset pagination)',
    end_to_end_id     CHAR(32)        NOT NULL    COMMENT 'EndToEndId padrão BACEN — único por PIX',
    idempotency_key   CHAR(36)        NULL        COMMENT 'UUID de idempotência (RN02) — CHAR(36)',
    source_account_id CHAR(36)        NOT NULL    COMMENT 'FK — conta de origem (quem paga)',
    target_account_id CHAR(36)        NOT NULL    COMMENT 'FK — conta de destino (quem recebe)',
    amount            DECIMAL(15,2)   NOT NULL    COMMENT 'Valor da transação em BRL',
    direction         ENUM('DEBIT', 'CREDIT')
                                      NOT NULL    COMMENT 'Débito na origem, Crédito no destino',
    description       VARCHAR(255)    NULL        COMMENT 'Descrição/mensagem do PIX (snapshot — RN05)',
    receiver_name_snapshot VARCHAR(120) NOT NULL   COMMENT 'Nome do recebedor congelado no momento (RN05)',
    receiver_doc_snapshot  VARCHAR(14) NOT NULL   COMMENT 'Documento mascarado do recebedor (RN05)',
    status            ENUM('PENDING', 'COMPLETED', 'REVERTED', 'FAILED')
                                      NOT NULL DEFAULT 'PENDING',
    created_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

    CONSTRAINT pk_transactions PRIMARY KEY (transaction_id),
    CONSTRAINT uq_e2e_id UNIQUE (end_to_end_id),
    CONSTRAINT uq_idempotency_key UNIQUE (idempotency_key),
    CONSTRAINT fk_tx_source_account FOREIGN KEY (source_account_id)
        REFERENCES accounts (account_id) ON DELETE RESTRICT,
    CONSTRAINT fk_tx_target_account FOREIGN KEY (target_account_id)
        REFERENCES accounts (account_id) ON DELETE RESTRICT,
    CONSTRAINT chk_amount_positive CHECK (amount > 0.00)
) ENGINE=InnoDB
  COMMENT='Transações PIX (imutáveis — RN05)';

-- Índices para consultas performáticas
CREATE INDEX ix_tx_source_created ON transactions (source_account_id, created_at DESC);
CREATE INDEX ix_tx_target_created ON transactions (target_account_id, created_at DESC);

-- ============================================================
-- 5. Tabela: audit_log
-- ============================================================
-- Populate pelo trigger AFTER INSERT em transactions.
-- Registro append-only para compliance/auditoria.

CREATE TABLE IF NOT EXISTS audit_log (
    audit_id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    transaction_id    BIGINT UNSIGNED NOT NULL,
    end_to_end_id     CHAR(32)        NOT NULL,
    source_account_id CHAR(36)        NOT NULL,
    target_account_id CHAR(36)        NOT NULL,
    amount            DECIMAL(15,2)   NOT NULL,
    action            VARCHAR(50)     NOT NULL DEFAULT 'PIX_TRANSFER_CREATED',
    audited_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

    CONSTRAINT pk_audit_log PRIMARY KEY (audit_id),
    CONSTRAINT fk_audit_transaction FOREIGN KEY (transaction_id)
        REFERENCES transactions (transaction_id) ON DELETE CASCADE
) ENGINE=InnoDB
  COMMENT='Log de auditoria — append only, populate por trigger';

-- ============================================================
-- 6. Trigger: Auditoria automática em transactions
-- ============================================================

DELIMITER $$

DROP TRIGGER IF EXISTS trg_transaction_audit$$
CREATE TRIGGER trg_transaction_audit
AFTER INSERT ON transactions
FOR EACH ROW
BEGIN
    INSERT INTO audit_log (transaction_id, end_to_end_id, source_account_id, target_account_id, amount, action)
    VALUES (NEW.transaction_id, NEW.end_to_end_id, NEW.source_account_id, NEW.target_account_id, NEW.amount, 'PIX_TRANSFER_CREATED');
END$$

DELIMITER ;

-- ============================================================
-- 7. Stored Procedure: sp_process_pix_transfer
-- ============================================================
-- Responsabilidade (CONSTITUTION — Lei II.1):
--   "Lógica financeira no banco. A API apenas orquestra."
--
-- Fluxo:
--   1. Valida idempotência (idempotency_key já existe?)
--   2. Lock pessimista na conta origem (FOR UPDATE NOWAIT)
--   3. Valida saldo suficiente
--   4. Debite na origem (UPDATE balance)
--   5. Credite no destino (UPDATE balance) — se conta destino existir
--   6. Insere par de transações (DEBIT + CREDIT)
--   7. Retorna status via parâmetros OUT
--
-- Atomicidade garantida pelo TRANSACTION do MySQL.
-- NOWAIT previne deadlocks prolongados (CONSTITUTION — Lei II.3).

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_process_pix_transfer$$
CREATE PROCEDURE sp_process_pix_transfer(
    -- Input Parameters
    IN  p_end_to_end_id       CHAR(32),
    IN  p_idempotency_key     CHAR(36),
    IN  p_source_account_id   CHAR(36),
    IN  p_target_account_id   CHAR(36),
    IN  p_amount              DECIMAL(15,2),
    IN  p_description         VARCHAR(255),
    IN  p_receiver_name       VARCHAR(120),
    IN  p_receiver_doc_masked VARCHAR(14),
    -- Output Parameters
    OUT p_result_code         INT,          -- 0=OK, 1=Idempotente(retornado), 2=Saldo insuficiente, 3=Lock failed, 4=Conta não encontrada, 5=Conta inativa, 6=Mesma conta
    OUT p_result_message      VARCHAR(255),
    OUT p_existing_e2e_id     CHAR(32)      -- Se idempotente, retorna o E2E ID original
)
proc_body: BEGIN
    DECLARE v_source_balance  DECIMAL(15,2);
    DECLARE v_source_status   VARCHAR(20);
    DECLARE v_target_status   VARCHAR(20);
    DECLARE v_existing_e2e    CHAR(32) DEFAULT NULL;
    DECLARE v_lock_acquired   INT DEFAULT 0;

    -- Handler para lock timeout (NOWAIT lança ER_LOCK_NOWAIT = 3572)
    DECLARE EXIT HANDLER FOR 3572
    BEGIN
        ROLLBACK;
        SET p_result_code = 3;
        SET p_result_message = 'Não foi possível obter lock na conta de origem. Tente novamente.';
        SET p_existing_e2e_id = NULL;
    END;

    -- Handler genérico para SQL exceptions (rollback automático)
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET p_result_code = 99;
        SET p_result_message = 'Erro interno ao processar transferência.';
        SET p_existing_e2e_id = NULL;
    END;

    -- Inicializa OUTs
    SET p_result_code = 0;
    SET p_result_message = 'Transferência processada com sucesso.';
    SET p_existing_e2e_id = NULL;

    -- ========================================================
    -- STEP 1: Idempotência (RN02 / CONSTITUTION Lei II.2)
    -- ========================================================
    SELECT end_to_end_id INTO v_existing_e2e
    FROM transactions
    WHERE idempotency_key = p_idempotency_key
      AND status = 'COMPLETED'
    LIMIT 1;

    IF v_existing_e2e IS NOT NULL THEN
        -- Idempotente: retorna o resultado original sem reprocessar
        SET p_result_code = 1;
        SET p_result_message = 'Transferência idempotente — já processada anteriormente.';
        SET p_existing_e2e_id = v_existing_e2e;
        LEAVE proc_body;
    END IF;

    -- ========================================================
    -- STEP 2: Validações pré-transação
    -- ========================================================

    -- Não permite transferir para si mesmo
    IF p_source_account_id = p_target_account_id THEN
        SET p_result_code = 6;
        SET p_result_message = 'Conta de origem e destino não podem ser iguais.';
        LEAVE proc_body;
    END IF;

    -- ========================================================
    -- STEP 3: Transação atômica (RN01)
    -- ========================================================
    START TRANSACTION;

    -- Lock pessimista na conta de origem (FOR UPDATE NOWAIT)
    -- NOWAIT: falha imediatamente se já está travada (previne deadlocks — Lei II.3)
    SELECT balance, status INTO v_source_balance, v_source_status
    FROM accounts
    WHERE account_id = p_source_account_id
      AND deleted_at IS NULL
    FOR UPDATE NOWAIT;

    -- Conta origem existe?
    IF v_source_status IS NULL THEN
        ROLLBACK;
        SET p_result_code = 4;
        SET p_result_message = 'Conta de origem não encontrada.';
        LEAVE proc_body;
    END IF;

    -- Conta origem está ativa?
    IF v_source_status != 'ACTIVE' THEN
        ROLLBACK;
        SET p_result_code = 5;
        SET p_result_message = 'Conta de origem não está ativa.';
        LEAVE proc_body;
    END IF;

    -- Valida saldo suficiente (RN03 — Sem cheque especial)
    IF v_source_balance < p_amount THEN
        ROLLBACK;
        SET p_result_code = 2;
        SET p_result_message = CONCAT('Saldo insuficiente. Disponível: R$ ', CAST(v_source_balance AS CHAR), '. Necessário: R$ ', CAST(p_amount AS CHAR), '.');
        LEAVE proc_body;
    END IF;

    -- Valida conta destino
    SELECT status INTO v_target_status
    FROM accounts
    WHERE account_id = p_target_account_id
      AND deleted_at IS NULL
    FOR UPDATE NOWAIT;

    IF v_target_status IS NULL THEN
        ROLLBACK;
        SET p_result_code = 4;
        SET p_result_message = 'Conta de destino não encontrada.';
        LEAVE proc_body;
    END IF;

    IF v_target_status != 'ACTIVE' THEN
        ROLLBACK;
        SET p_result_code = 5;
        SET p_result_message = 'Conta de destino não está ativa.';
        LEAVE proc_body;
    END IF;

    -- ========================================================
    -- STEP 4: Débito na conta origem
    -- ========================================================
    UPDATE accounts
    SET balance = balance - p_amount
    WHERE account_id = p_source_account_id;

    -- ========================================================
    -- STEP 5: Crédito na conta destino
    -- ========================================================
    UPDATE accounts
    SET balance = balance + p_amount
    WHERE account_id = p_target_account_id;

    -- ========================================================
    -- STEP 6: Registra transação DEBIT (origem)
    -- ========================================================
    INSERT INTO transactions (
        end_to_end_id, idempotency_key,
        source_account_id, target_account_id,
        amount, direction, description,
        receiver_name_snapshot, receiver_doc_snapshot,
        status
    ) VALUES (
        p_end_to_end_id, p_idempotency_key,
        p_source_account_id, p_target_account_id,
        p_amount, 'DEBIT', p_description,
        p_receiver_name, p_receiver_doc_masked,
        'COMPLETED'
    );

    -- ========================================================
    -- STEP 7: Registra transação CREDIT (destino) — par contábil
    -- ========================================================
    INSERT INTO transactions (
        end_to_end_id, idempotency_key,
        source_account_id, target_account_id,
        amount, direction, description,
        receiver_name_snapshot, receiver_doc_snapshot,
        status
    ) VALUES (
        -- E2E ID do par de crédito tem sufixo 'C' para distingui-lo, mas compartilha o mesmo idempotency_key
        CONCAT(LEFT(p_end_to_end_id, 31), 'C'),
        NULL,  -- Apenas o DEBIT carrega a idempotency key (é a transação "raiz")
        p_source_account_id, p_target_account_id,
        p_amount, 'CREDIT', p_description,
        p_receiver_name, p_receiver_doc_masked,
        'COMPLETED'
    );

    -- ========================================================
    -- STEP 8: Commit atômico
    -- ========================================================
    COMMIT;

    SET p_result_code = 0;
    SET p_result_message = 'Transferência processada com sucesso.';
    SET p_existing_e2e_id = p_end_to_end_id;

END$$

DELIMITER ;

-- ============================================================
-- 8. View: vw_account_statement (Extrato com Mascaramento)
-- ============================================================
-- Usada para consulta de extrato (RF04).
-- Mascara nome do recebedor e documento (RN04 — Transparência PIX).
-- O ORDER BY é feito na query da aplicação (keyset pagination).

CREATE OR REPLACE VIEW vw_account_statement AS
SELECT
    t.transaction_id,
    t.end_to_end_id,
    t.source_account_id,
    t.target_account_id,
    t.amount,
    t.direction,
    t.description,
    t.status,
    t.created_at,
    -- Mascaramento do nome: mostra apenas 2 chars iniciais + ***
    CASE
        WHEN CHAR_LENGTH(t.receiver_name_snapshot) <= 2
            THEN t.receiver_name_snapshot
        ELSE CONCAT(LEFT(t.receiver_name_snapshot, 2), '***')
    END AS masked_receiver_name,
    -- Documento já vem mascarado da tabela (snapshot)
    t.receiver_doc_snapshot AS masked_receiver_doc,
    -- Sinaliza se é "enviado" ou "recebido" do ponto de vista da conta consultada
    CASE
        WHEN t.direction = 'DEBIT'  THEN t.target_account_id
        WHEN t.direction = 'CREDIT' THEN t.source_account_id
    END AS counterparty_account_id,
    -- Determina a conta "dona" desta linha de extrato
    CASE
        WHEN t.direction = 'DEBIT'  THEN t.source_account_id
        WHEN t.direction = 'CREDIT' THEN t.target_account_id
    END AS owner_account_id
FROM transactions t
WHERE t.status = 'COMPLETED';

-- ============================================================
-- 9. Dados de Seed (para desenvolvimento)
-- ============================================================
-- Insere 2 contas de teste com CPF hasheado (VARBINARY via X'...').
-- GUIDs como CHAR(36) formatado (padrão UUID canônico do Pomelo/EF).

INSERT IGNORE INTO accounts (account_id, holder_name, holder_email, holder_cpf_hash, balance, status)
VALUES
    -- Account A: João Silva (saldo R$ 10.000)
    (
        '550e8400-e29b-41d4-a716-446655440000',
        'João Silva',
        'joao@test.com',
        X'6ca13d52ca70c883e0f0bb101e425a89e8624de51db2d4b31f02cbb23b99a357',
        10000.00,
        'ACTIVE'
    ),
    -- Account B: Maria Santos (saldo R$ 5.000)
    (
        '6ba7b810-98ad-4116-a947-2de217cfe384',
        'Maria Santos',
        'maria@test.com',
        X'60303ae22b99842ebd42c580a9d230d5e4c0f6b6e2e1c0a4e3a55e5e83b5b39a',
        5000.00,
        'ACTIVE'
    );

-- ============================================================
-- FIM DO SCRIPT
-- ============================================================
-- Validação pós-init:
--   SHOW TABLES;
--   SHOW PROCEDURE STATUS WHERE Db = 'banking_core';
--   SHOW FULL TABLES IN banking_core WHERE TABLE_TYPE LIKE 'VIEW';
--   SHOW TRIGGERS FROM banking_core;
-- ============================================================
