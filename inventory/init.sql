CREATE TABLE "products" (
	"id" integer PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "products_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 START WITH 1 CACHE 1),
	"name" varchar(255) NOT NULL,
	"quantity" integer NOT NULL,
	CONSTRAINT "products_name_unique" UNIQUE("name"),
	CONSTRAINT "quantity_check" CHECK ("products"."quantity" >= 0)
);

INSERT INTO products (
  name,
  quantity
) VALUES (
  'Plans for Constructing an Orgone Energy Accumulator', 
  3
);
