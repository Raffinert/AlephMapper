SELECT COALESCE("d"."Name", 'Unassigned')
FROM "Employees" AS "e"
LEFT JOIN "Departments" AS "d" ON "e"."DepartmentId" = "d"."Id"
ORDER BY "e"."Id"