# saga-orchestrator

## Running

```bash
docker compose up -d
# Give it 15 seconds for all services to start
curl -X POST -H "Content-Type: application/json" \
  -d '{"CustomerId": 1, "ProductId": 1, "Amount": 8.50}' \
  http://localhost:5000/order
```

### Checking order status

```bash
curl -X GET http://localhost:5000/order/{orderId}
```

### Checking saga status

```bash
curl -X GET http://localhost:5001/saga/{sagaId}
```

## Notes

- I know, I know, sending `Amount` isn't a good idea because the user can change it, but this is just for the sake of learning.
- I should have used a library for data access such as `Dapper.FSharp` or `Npgsql.FSharp`, you know, there's a lot of boilerplate code in my `Database.fs` files, I'll fix this in the next projects.
