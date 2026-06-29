Project purpose

The aim of the system is to allow localities to manage school assistants. 
The school assistants are managed per school year - expressed in Jewish years.
There are different types of school assistants (class/school/student level)
In order to employ an assistant (סייעת) there needs to be an entitlement (זכאות) for that year. The entitlement includes, school, number of hours, start and end date (default based on the school year), type of assistant and student(if relevant)
It should be possible to manage people in the system that are a assistants of other roles. people may have multiple roles.
assistants will be allocated to an entitlement with a start and end date, that must be in the range of the entitlement.

Arhitecture
> **Multi-tenancy**: Each local authority's data must be strictly isolated — users of authority A must never see authority B's data. Local authorities are modelled as `entities` in the system; schools and other organisational units are also entities with their own type.
>
> **Shared (global) configuration**: Some reference data is shared across all tenants — entity types, assistant types, cities, the list of authorities themselves, and similar lookup tables. Security actions appear here. system attributes as well.  This data is managed centrally and read by all.
>
> **Tenant-specific configuration**: Some configuration belongs to a specific local authority — users, user roles, permission sets, local settings, and others to be defined.
>
> **Cross-entity persons**: A person (e.g. an assistant) may exist in more than one local authority. There is no shared identity record — each local authority holds a fully isolated copy of the person's data, with no linkage enforced at the database level between authorities. If the same physical person works for two authorities, they appear as two independent records, each fully owned by their respective authority. Any deduplication or identity matching, if needed in future, is an application-layer concern, not a schema constraint.
>
> **SaaS deployment**: The application will be deployed as a SaaS product. The multi-tenancy model must scale horizontally with tenant count at zero operational overhead per new tenant. Schema-per-tenant is explicitly excluded. The required approach is two fixed PostgreSQL schemas regardless of tenant count — `shared_schema` for global reference data with no tenant ownership, and `assist_schema` for all operational data with mandatory `entity_id` on every table, filtered at the EF Core query level via global query filters. Adding a new local authority is an `INSERT` into `shared_schema.entities` only, with no schema or infrastructure changes.

>**Encryption**: ID's must be encrypted in the database, same goes for emails
>


