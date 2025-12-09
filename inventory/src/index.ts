import 'dotenv/config';
import { drizzle } from 'drizzle-orm/node-postgres';
import { productsTable } from './db/schema.ts';
import { Kafka } from '@confluentinc/kafka-javascript/types/kafkajs.js';
import { eq, sql } from 'drizzle-orm';

const db = drizzle(process.env.DATABASE_URL);

const producer = new Kafka().producer({
  'bootstrap.servers': process.env.KAFKA_SERVER
});

const consumer = new Kafka().consumer({
  'bootstrap.servers': process.env.KAFKA_SERVER,
  'group.id': 'inventory-consumer'
});

await producer.connect();
await consumer.connect();
// TODO: Read from environment variable
await consumer.subscribe({ topics: [""] });

interface Order {
  orderId: string;
  customerId: number;
  productId: number;
  amount: number;
}

interface SagaMessage {
  sagaId: string;
  type: string;
  order: Order;
}

consumer.run({
  eachMessage: async ({ topic, partition, message }) => {
    const sagaMessage: SagaMessage = JSON.parse(message.value.toString());
    const orderId = sagaMessage.order.orderId;
    const productId = sagaMessage.order.productId;

    if (sagaMessage.type === 'ReserveInventory') {
      // TODO: Check and update database

      const [result] = await db
        .select({ quantity: productsTable.quantity })
        .from(productsTable)
        .where(eq(productsTable.id, productId))
        .limit(1);

      if (result.quantity > 0) {
        await db.update(productsTable)
          .set({ quantity: sql`${productsTable.quantity} - 1` })
          .where(eq(productsTable.id, productId));

        sagaMessage.type = 'InventoryReserved';

        await producer.send({
          topic: 'orchestrator',
          messages: [
            {
              key: orderId,
              value: JSON.stringify(sagaMessage)
            }
          ]
        })
      } else {
        sagaMessage.type = 'OutOfStock';

        await producer.send({
          topic: 'orchestrator',
          messages: [
            {
              key: orderId,
              value: JSON.stringify(sagaMessage)
            }
          ]
        });
      }
    }
  }
});

await producer.disconnect();
await consumer.disconnect();
