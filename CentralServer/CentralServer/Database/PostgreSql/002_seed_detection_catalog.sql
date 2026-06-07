-- Initial dictionaries for zones, roles, permissions, and detection types.

INSERT INTO access.roles (key, name, scope)
VALUES
    ('company-admin', 'Администратор компании', 'company'),
    ('company-operator', 'Оператор компании', 'company'),
    ('platform-admin', 'Администратор платформы', 'platform')
ON CONFLICT (key) DO UPDATE SET
    name = EXCLUDED.name,
    scope = EXCLUDED.scope,
    updated_at_utc = now();

INSERT INTO access.permissions (key, name, description)
VALUES
    ('sites.read', 'Просмотр точек', 'Просмотр точек компании и статусов Server.'),
    ('cameras.read', 'Просмотр камер', 'Просмотр камер и текущих кадров.'),
    ('archive.read', 'Просмотр архива', 'Просмотр motion/archive/evidence данных.'),
    ('zones.manage', 'Управление зонами', 'Создание, изменение и удаление зон.'),
    ('detection-profiles.manage', 'Управление моделями', 'Настройка профилей фиксаций и моделей.'),
    ('users.manage', 'Управление пользователями', 'Управление пользователями компании.')
ON CONFLICT (key) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description;

INSERT INTO access.role_permissions (role_id, permission_id)
SELECT role.id, permission.id
FROM access.roles role
JOIN access.permissions permission ON permission.key IN (
    'sites.read',
    'cameras.read',
    'archive.read',
    'zones.manage',
    'detection-profiles.manage',
    'users.manage'
)
WHERE role.key = 'company-admin'
ON CONFLICT DO NOTHING;

INSERT INTO access.role_permissions (role_id, permission_id)
SELECT role.id, permission.id
FROM access.roles role
JOIN access.permissions permission ON permission.key IN (
    'sites.read',
    'cameras.read',
    'archive.read'
)
WHERE role.key = 'company-operator'
ON CONFLICT DO NOTHING;

INSERT INTO catalog.zone_name_templates (key, name, zone_type_key, display_order)
VALUES
    ('stall', 'Прилавок', 'stall-zone', 10),
    ('client', 'Клиентская', 'client-zone', 20),
    ('cash-register', 'Касса', 'cash-register-zone', 30),
    ('smoke', 'Дым', 'smoke-zone', 40),
    ('phone', 'Телефон', 'phone-zone', 50),
    ('bottles', 'Бутылки', 'bottles-zone', 60),
    ('badge', 'Бейдж', 'badge-zone', 70),
    ('table', 'Стол', 'table-zone', 80),
    ('light', 'Свет', 'light-zone', 90),
    ('mopping', 'Мойка полов', 'mopping-zone', 100)
ON CONFLICT (key) DO UPDATE SET
    name = EXCLUDED.name,
    zone_type_key = EXCLUDED.zone_type_key,
    display_order = EXCLUDED.display_order,
    updated_at_utc = now();

INSERT INTO detection.detection_types (key, name, category, detection_kind, default_severity)
VALUES
    ('client-presence-test', 'Наличие клиента в клиентской зоне', 'context', 'yolo_object', 'low'),
    ('phone', 'Телефон', 'staff-control', 'yolo_object', 'medium'),
    ('bottles', 'Бутылки', 'staff-control', 'yolo_object', 'medium'),
    ('smoke', 'Дым', 'safety', 'yolo_object', 'high'),
    ('cash-register', 'Касса', 'cash-control', 'state_machine', 'high'),
    ('counting-cash-register', 'Пересчёт кассы', 'cash-control', 'state_machine', 'medium'),
    ('abandoned-open-cash-register', 'Открытая касса без сотрудника', 'cash-control', 'state_machine', 'high'),
    ('mopping', 'Мойка полов', 'cleaning', 'yolo_object', 'medium'),
    ('badge', 'Бейдж', 'staff-control', 'classifier', 'medium'),
    ('clothes', 'Форма сотрудника', 'staff-control', 'classifier', 'medium'),
    ('pose', 'Поза сотрудника', 'staff-control', 'pose_assisted', 'medium'),
    ('conversion', 'Конверсия', 'conversion', 'directional_tracking', 'low'),
    ('clear-stall', 'Чистый стол', 'cleaning', 'surface_difference', 'medium'),
    ('delays', 'Опоздания и ранний уход', 'schedule', 'business_state_machine', 'medium'),
    ('crowd', 'Скопление людей', 'client-control', 'people_count', 'medium'),
    ('light', 'Свет', 'environment', 'image_heuristic', 'medium'),
    ('service-near-cabinet', 'Обслуживание возле шкафа', 'staff-control', 'state_machine', 'medium'),
    ('no-one-at-stall', 'Нет сотрудника у прилавка', 'staff-control', 'state_machine', 'high'),
    ('human-before-after-shift', 'Человек вне смены', 'schedule', 'people_count', 'medium'),
    ('inactive-salesman', 'Неактивный продавец', 'staff-control', 'state_machine', 'medium')
ON CONFLICT (key) DO UPDATE SET
    name = EXCLUDED.name,
    category = EXCLUDED.category,
    detection_kind = EXCLUDED.detection_kind,
    default_severity = EXCLUDED.default_severity,
    updated_at_utc = now();

INSERT INTO detection.detection_type_parameters (
    detection_type_id,
    key,
    name,
    value_type,
    default_value,
    min_value,
    max_value,
    is_required,
    display_order
)
SELECT type.id, params.key, params.name, params.value_type, params.default_value::jsonb, params.min_value::jsonb, params.max_value::jsonb, params.is_required, params.display_order
FROM detection.detection_types type
CROSS JOIN (
    VALUES
        ('interval_seconds', 'Период вызова модели', 'int', '5', '1', '86400', true, 10),
        ('cooldown_seconds', 'Пауза между фиксациями', 'int', '30', '0', '86400', true, 20),
        ('confidence_threshold', 'Confidence модели', 'double', '0.4', '0', '1', false, 30),
        ('requires_client_zone_presence', 'Требуется клиент в клиентской зоне', 'bool', 'false', NULL, NULL, false, 40),
        ('save_evidence_on_positive_result', 'Сохранять фото при срабатывании', 'bool', 'true', NULL, NULL, true, 50),
        ('target_zone_type_key', 'Целевая зона', 'zone_type', 'null', NULL, NULL, false, 60),
        ('client_zone_type_key', 'Клиентская зона', 'zone_type', '"client-zone"', NULL, NULL, false, 70)
) AS params(key, name, value_type, default_value, min_value, max_value, is_required, display_order)
WHERE type.key IN ('client-presence-test', 'phone', 'bottles', 'smoke')
ON CONFLICT (detection_type_id, key) DO UPDATE SET
    name = EXCLUDED.name,
    value_type = EXCLUDED.value_type,
    default_value = EXCLUDED.default_value,
    min_value = EXCLUDED.min_value,
    max_value = EXCLUDED.max_value,
    is_required = EXCLUDED.is_required,
    display_order = EXCLUDED.display_order,
    updated_at_utc = now();
