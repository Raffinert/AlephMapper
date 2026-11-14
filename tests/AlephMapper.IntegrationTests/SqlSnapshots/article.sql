SELECT "a"."Id", "a"."Number", "a0"."FirstName" || ' ' || "a0"."LastName", (
    SELECT COALESCE(ef_sum(ef_multiply("a1"."Price", CAST("a1"."Quantity" AS TEXT))), '0.0')
    FROM "ArticleOrderLines" AS "a1"
    WHERE "a"."Id" = "a1"."OrderId")
FROM "ArticleOrders" AS "a"
INNER JOIN "ArticleCustomers" AS "a0" ON "a"."CustomerId" = "a0"."Id"
ORDER BY "a"."Id"