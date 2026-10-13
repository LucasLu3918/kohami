IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
GO
IF SCHEMA_ID('m0commerce') IS NULL EXEC('CREATE SCHEMA m0commerce');
GO
CREATE TABLE m0commerce.Documents (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Kind varchar(30) NOT NULL, Id varchar(100) NOT NULL,
 OwnerId varchar(150) NOT NULL, Version bigint NOT NULL CHECK(Version>0), Body nvarchar(max) NOT NULL CHECK(ISJSON(Body)=1),
 PRIMARY KEY(Dataset,Kind,Id));
CREATE TABLE m0commerce.Idempotency (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Actor varchar(150) NOT NULL, Scope varchar(200) NOT NULL, OperationKey varchar(160) NOT NULL,
 RequestHash char(64) NOT NULL, Response nvarchar(max) NOT NULL CHECK(ISJSON(Response)=1), StatusCode int NOT NULL, Version bigint NULL,
 PRIMARY KEY(Dataset,Actor,Scope,OperationKey));
CREATE TABLE m0commerce.Inventory (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Sku varchar(100) NOT NULL, Price bigint NOT NULL CHECK(Price>=0), Stock int NOT NULL CHECK(Stock>=0), Active bit NOT NULL DEFAULT 1,
 PRIMARY KEY(Dataset,Sku));
CREATE TABLE m0commerce.Reservations (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), OrderId varchar(100) NOT NULL, Sku varchar(100) NOT NULL,
 Quantity int NOT NULL CHECK(Quantity>0), State varchar(10) NOT NULL CHECK(State IN ('Held','Released','Consumed')),
 ExpiresAt datetimeoffset NOT NULL, PRIMARY KEY(Dataset,OrderId,Sku), FOREIGN KEY(Dataset,Sku) REFERENCES m0commerce.Inventory(Dataset,Sku));
CREATE TABLE m0commerce.Collections (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Id varchar(100) NOT NULL, OrderId varchar(100) NOT NULL,
 Kind varchar(10) NOT NULL CHECK(Kind IN ('Primary','Late','Extra')), Collected bigint NOT NULL CHECK(Collected>0), Pending bigint NOT NULL DEFAULT 0, Refunded bigint NOT NULL DEFAULT 0,
 ShippingPaid bigint NOT NULL DEFAULT 0, ShippingSlot varchar(15) NOT NULL DEFAULT 'Free' CHECK(ShippingSlot IN ('Free','Processing','Unknown','Succeeded')),
 PRIMARY KEY(Dataset,Id), CHECK(Pending>=0 AND Refunded>=0 AND Pending+Refunded<=Collected), CHECK(ShippingPaid>=0 AND ShippingPaid<=Collected));
CREATE TABLE m0commerce.RefundUnits (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), UnitId varchar(100) NOT NULL, OrderId varchar(100) NOT NULL, LineId varchar(100) NOT NULL,
 CollectionId varchar(100) NOT NULL, Ordinal int NOT NULL CHECK(Ordinal>=0), Paid bigint NOT NULL CHECK(Paid>=0),
 Slot varchar(15) NOT NULL DEFAULT 'Free' CHECK(Slot IN ('Free','Processing','Unknown','Succeeded')), OperationId varchar(100) NULL,
 PRIMARY KEY(Dataset,UnitId), UNIQUE(Dataset,OrderId,LineId,Ordinal), FOREIGN KEY(Dataset,CollectionId) REFERENCES m0commerce.Collections(Dataset,Id));
CREATE TABLE m0commerce.Jobs (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Id varchar(100) NOT NULL, Kind varchar(30) NOT NULL, TargetId varchar(100) NOT NULL,
 Actor varchar(150) NOT NULL, SecurityVersion bigint NOT NULL, Permission varchar(80) NULL, ResourceKind varchar(50) NOT NULL, ResourceId varchar(100) NOT NULL,
 State varchar(15) NOT NULL CHECK(State IN ('NotStarted','Started','Completed','Blocked')), LeaseOwner varchar(100) NULL, LeaseUntil datetimeoffset NULL, Fence bigint NOT NULL DEFAULT 0,
 Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1), CreatedAt datetimeoffset NOT NULL,
 PRIMARY KEY(Dataset,Id), UNIQUE(Dataset,Kind,TargetId));
CREATE TABLE m0commerce.Inbox (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), EventId varchar(100) NOT NULL, SourceId varchar(100) NOT NULL,
 PayloadHash char(64) NOT NULL, Body nvarchar(max) NOT NULL CHECK(ISJSON(Body)=1), Applied bit NOT NULL DEFAULT 0,
 PRIMARY KEY(Dataset,EventId), UNIQUE(Dataset,SourceId));
CREATE TABLE m0commerce.GuestRequests (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Id varchar(100) NOT NULL, OrderId varchar(100) NOT NULL, BrowserId varchar(150) NOT NULL,
 Intent varchar(10) NOT NULL CHECK(Intent IN ('read','cancel','return','claim')), NonceHash char(64) NOT NULL, ProofHash char(64) NOT NULL,
 ExpiresAt datetimeoffset NOT NULL, Used bit NOT NULL DEFAULT 0, Matched bit NOT NULL,
 PRIMARY KEY(Dataset,Id));
CREATE TABLE m0commerce.PrivateDeliveries (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Id varchar(100) NOT NULL, RequestId varchar(100) NOT NULL, ProtectedProof nvarchar(max) NOT NULL,
 PRIMARY KEY(Dataset,Id), FOREIGN KEY(Dataset,RequestId) REFERENCES m0commerce.GuestRequests(Dataset,Id));
CREATE TABLE m0commerce.GuestCapabilities (
 Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'), Id varchar(100) NOT NULL, OrderId varchar(100) NOT NULL, BrowserId varchar(150) NOT NULL,
 Intent varchar(10) NOT NULL CHECK(Intent IN ('read','cancel','return','claim')), ExpiresAt datetimeoffset NOT NULL, Consumed bit NOT NULL DEFAULT 0, OperationKey varchar(160) NULL,
 PRIMARY KEY(Dataset,Id));
GO
