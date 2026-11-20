# ADR 0003 · EF Core visible en la capa Application

**Estado:** aceptada

## Contexto
La Clean Architecture "pura" esconde el ORM detrás de repositorios genéricos. En la práctica eso
duplica lo que `DbSet<T>` ya ofrece (unidad de trabajo, consultas componibles) y empuja las
proyecciones a Infrastructure.

## Decisión
Application depende de `Microsoft.EntityFrameworkCore` (sin proveedor) a través de `IAppDbContext`.
El proveedor (SQL Server o SQLite), las configuraciones y el Outbox viven en Infrastructure.

## Consecuencias
- Consultas expresivas y eficientes (proyecciones a DTO en la base) sin capas vacías.
- Las pruebas de integración usan SQLite en memoria con el mismo modelo.
- Cambiar de ORM implicaría tocar Application; se acepta por el costo y la probabilidad bajos.
