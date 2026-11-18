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
    name varchar NOT NULL UNIQUE,
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
    approved_at timestamp,
    CONSTRAINT stop_coordinates_unique UNIQUE (coordinatex, coordinatey)
);

CREATE TABLE IF NOT EXISTS fyi.itinerary (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    title varchar NOT NULL UNIQUE,
    description varchar NOT NULL,
    status varchar NOT NULL,
    is_customizable boolean NOT NULL,
    created_by uuid REFERENCES fyi.users (id),
    updated_by uuid REFERENCES fyi.users (id),
    created_at timestamp,
    updated_at timestamp
);

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
