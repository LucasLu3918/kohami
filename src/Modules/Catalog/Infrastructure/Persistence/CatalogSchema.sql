IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
GO
CREATE TABLE m0.CatalogProducts (
 Dataset varchar(30) NOT NULL CHECK (Dataset='demo_m0'),
 ProductId varchar(100) NOT NULL,
 Slug varchar(100) NOT NULL,
 PublicSlug varchar(100) NULL,
 State varchar(20) NOT NULL CHECK (State IN ('Draft','Published','Unpublished')),
 Version bigint NOT NULL CHECK (Version>0),
 PublicRevision varchar(100) NULL,
 DraftJson nvarchar(max) NOT NULL CHECK (ISJSON(DraftJson)=1),
 PublicJson nvarchar(max) NULL CHECK (PublicJson IS NULL OR ISJSON(PublicJson)=1),
 OwnerId varchar(150) NOT NULL,
 CreatedAt datetimeoffset NOT NULL,
 UpdatedAt datetimeoffset NOT NULL,
 PRIMARY KEY (Dataset,ProductId),
 UNIQUE (Dataset,Slug)
);
CREATE UNIQUE INDEX UX_CatalogProducts_PublicSlug ON m0.CatalogProducts(Dataset,PublicSlug) WHERE PublicSlug IS NOT NULL;
CREATE TABLE m0.CatalogProductSkus (
 Dataset varchar(30) NOT NULL CHECK (Dataset='demo_m0'),
 SkuId varchar(100) NOT NULL,
 ProductId varchar(100) NOT NULL,
 ColorId varchar(80) NOT NULL,
 GarmentSize varchar(40) NOT NULL,
 DraftPriceTwd bigint NOT NULL CHECK (DraftPriceTwd>0),
 DraftActive bit NOT NULL,
 DraftIncluded bit NOT NULL,
 PublicPriceTwd bigint NULL CHECK (PublicPriceTwd IS NULL OR PublicPriceTwd>0),
 PublicActive bit NULL,
 PublicIncluded bit NOT NULL DEFAULT 0,
 PRIMARY KEY (Dataset,SkuId),
 UNIQUE (Dataset,ProductId,ColorId,GarmentSize),
 FOREIGN KEY (Dataset,ProductId) REFERENCES m0.CatalogProducts(Dataset,ProductId),
 FOREIGN KEY (Dataset,SkuId) REFERENCES m0commerce.Inventory(Dataset,Sku)
);
CREATE TABLE m0.CatalogAuditEvents (
 Dataset varchar(30) NOT NULL CHECK (Dataset='demo_m0'),
 AuditId varchar(100) NOT NULL,
 ProductId varchar(100) NOT NULL,
 ActorId varchar(150) NOT NULL,
 Action varchar(40) NOT NULL CHECK (Action IN ('ProductCreated','ProductDraftUpdated','ProductPublished','ProductUnpublished')),
 Outcome varchar(20) NOT NULL CHECK (Outcome='Succeeded'),
 Version bigint NOT NULL,
 At datetimeoffset NOT NULL,
 Details nvarchar(max) NOT NULL CHECK (ISJSON(Details)=1),
 PRIMARY KEY (Dataset,AuditId),
 FOREIGN KEY (Dataset,ProductId) REFERENCES m0.CatalogProducts(Dataset,ProductId)
);
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0.CatalogProducts TO m0_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0.CatalogProductSkus TO m0_api;
GRANT SELECT,INSERT ON OBJECT::m0.CatalogAuditEvents TO m0_api;
GRANT SELECT ON OBJECT::m0.CatalogProducts TO m0_native;
GRANT INSERT ON OBJECT::m0commerce.Inventory TO m0_api;
GO
