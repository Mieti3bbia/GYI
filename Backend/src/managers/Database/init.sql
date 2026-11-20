CREATE SCHEMA IF NOT EXISTS fyi;

CREATE TABLE IF NOT EXISTS fyi.roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar NOT NULL
);

CREATE TABLE IF NOT EXISTS fyi.users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    firstname varchar NOT NULL,
    lastname varchar NOT NULL,
    email varchar NOT NULL UNIQUE,
    password varchar NOT NULL, -- store password hashes
    role_id uuid NOT NULL REFERENCES fyi.roles (id),
    created_at timestamp,
    updated_at timestamp
);

CREATE TABLE IF NOT EXISTS fyi.stop (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar NOT NULL,
    description varchar NOT NULL,
    status varchar NOT NULL,
    cap numeric NOT NULL,
    price numeric NOT NULL CHECK (price >= 0),
    coordinatex numeric NOT NULL,
    coordinatey numeric NOT NULL,
    proposed_by uuid NOT NULL REFERENCES fyi.users (id),
    created_at timestamp,
    updated_at timestamp,
    approved_by uuid REFERENCES fyi.users (id),
    approved_at timestamp
);

ALTER TABLE fyi.stop
    ADD COLUMN IF NOT EXISTS is_fixed boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS municipality varchar,
    ADD COLUMN IF NOT EXISTS province varchar,
    ADD COLUMN IF NOT EXISTS source_url varchar,
    DROP CONSTRAINT IF EXISTS stop_name_key,
    DROP CONSTRAINT IF EXISTS stop_coordinates_unique;

-- Distinct catalog sites may share a name or a building. Keep both legacy constraints for manual stops.
CREATE UNIQUE INDEX IF NOT EXISTS stop_manual_name_unique ON fyi.stop (name) WHERE source_url IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS stop_manual_coordinates_unique
    ON fyi.stop (coordinatex, coordinatey) WHERE source_url IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS stop_source_url_unique ON fyi.stop (source_url);

CREATE TABLE IF NOT EXISTS fyi.itinerary (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    title varchar NOT NULL UNIQUE,
    theme varchar,
    description varchar NOT NULL,
    geographic_area varchar,
    status varchar NOT NULL,
    is_customizable boolean NOT NULL,
    created_by uuid REFERENCES fyi.users (id),
    updated_by uuid REFERENCES fyi.users (id),
    created_at timestamp,
    updated_at timestamp
);

ALTER TABLE fyi.itinerary
    ADD COLUMN IF NOT EXISTS theme varchar,
    ADD COLUMN IF NOT EXISTS geographic_area varchar;

UPDATE fyi.itinerary SET status = 'pending' WHERE status IN ('Draft', 'draft');

CREATE TABLE IF NOT EXISTS fyi.itinerary_stops (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    itinerary_id uuid NOT NULL REFERENCES fyi.itinerary (id),
    stop_id uuid NOT NULL REFERENCES fyi.stop (id),
    "order" numeric NOT NULL,
    created_by uuid REFERENCES fyi.users (id),
    updated_by uuid REFERENCES fyi.users (id),
    created_at timestamp,
    updated_at timestamp
);

CREATE INDEX IF NOT EXISTS itinerary_stops_itinerary_order
    ON fyi.itinerary_stops (itinerary_id, "order");
