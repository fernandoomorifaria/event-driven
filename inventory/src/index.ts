import 'dotenv/config';
import { drizzle } from 'drizzle-orm/node-postgres';
import { productsTable } from './db/schema.ts';
import { KafkaJS } from '@confluentinc/kafka-javascript';
import { eq, sql } from 'drizzle-orm';

const db = drizzle(process.env.DATABASE_URL);

const producer = new KafkaJS.Kafka().producer({
  'bootstrap.servers': process.env.KAFKA_SERVER
});

const consumer = new KafkaJS.Kafka().consumer({
  'bootstrap.servers': process.env.KAFKA_SERVER,
  'group.id': 'inventory-consumer'
});

const topic = process.env.KAFKA_TOPIC;
const replyTopic = process.env.KAFKA_REPLY_TOPIC;

await producer.connect();
await consumer.connect();

await consumer.subscribe({ topics: [topic] });

interface InventoryCommand {
  sagaId: string;
  orderId: string
  type: string
  productId: number
}

interface Reply {
  sagaId: string;
  orderId: string;
  type: string;
}

await consumer.run({
  eachMessage: async ({ message }) => {
    const command: InventoryCommand = JSON.parse(message.value.toString());
    const orderId = command.orderId;
    const productId = command.productId;

    const reply: Reply = {
      sagaId: command.sagaId,
      orderId: orderId,
      type: ''
    };

    if (command.type === 'inventory.reserve') {
      const [result] = await db
        .select({ quantity: productsTable.quantity })
        .from(productsTable)
        .where(eq(productsTable.id, productId))
        .limit(1);

      if (result.quantity > 0) {
        await db.update(productsTable)
          .set({ quantity: sql`${productsTable.quantity} - 1` })
          .where(eq(productsTable.id, productId));

        reply.type = 'inventory.reserved';

        await producer.send({
          topic: replyTopic,
          messages: [
            {
              key: orderId,
              value: JSON.stringify(reply)
            }
          ]
        });
      } else {
        reply.type = 'inventory.out_of_stock';

        await producer.send({
          topic: replyTopic,
          messages: [
            {
              key: orderId,
              value: JSON.stringify(reply)
            }
          ]
        });
      }
    }
    if (command.type === 'inventory.release') {
      await db.update(productsTable)
        .set({ quantity: sql`${productsTable.quantity} + 1` })
        .where(eq(productsTable.id, productId));

      reply.type = 'inventory.released';

      await producer.send({
        topic: replyTopic,
        messages: [
          {
            key: orderId,
            value: JSON.stringify(reply)
          }
        ]
      });
    }
  }
});
