import { sql } from 'drizzle-orm';
import { check, integer, pgTable, varchar } from 'drizzle-orm/pg-core';

export const productsTable = pgTable(
  "products",
  {
    id: integer().primaryKey().generatedAlwaysAsIdentity(),
    name: varchar({ length: 255 }).notNull().unique(),
    quantity: integer().notNull()
  },
  (t) => [
    check("quantity_check", sql`${t.quantity} >= 0`)
  ]
);
