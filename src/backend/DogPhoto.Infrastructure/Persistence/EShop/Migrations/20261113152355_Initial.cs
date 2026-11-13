using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DogPhoto.Infrastructure.Persistence.EShop.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "eshop");

            migrationBuilder.CreateTable(
                name: "formats",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending_payment"),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GoPayPaymentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShippingAddressJson = table.Column<string>(type: "jsonb", nullable: true),
                    BillingAddressJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "paper_types",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameSk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    IsLimitedEdition = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EditionSize = table.Column<int>(type: "integer", nullable: true),
                    EditionSold = table.Column<int>(type: "integer", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shopping_carts",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_carts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    EditionNumber = table.Column<int>(type: "integer", nullable: true),
                    ProductTitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProductTitleEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FormatNameSk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FormatNameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaperTypeNameSk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaperTypeNameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "eshop",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormatId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_variants_formats_FormatId",
                        column: x => x.FormatId,
                        principalSchema: "eshop",
                        principalTable: "formats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_variants_paper_types_PaperTypeId",
                        column: x => x.PaperTypeId,
                        principalSchema: "eshop",
                        principalTable: "paper_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_variants_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "eshop",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                schema: "eshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cart_items_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "eshop",
                        principalTable: "product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cart_items_shopping_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "eshop",
                        principalTable: "shopping_carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId",
                schema: "eshop",
                table: "cart_items",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_VariantId",
                schema: "eshop",
                table: "cart_items",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_formats_Code",
                schema: "eshop",
                table: "formats",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrderId",
                schema: "eshop",
                table: "order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_types_Code",
                schema: "eshop",
                table: "paper_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_FormatId",
                schema: "eshop",
                table: "product_variants",
                column: "FormatId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_PaperTypeId",
                schema: "eshop",
                table: "product_variants",
                column: "PaperTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId_FormatId_PaperTypeId",
                schema: "eshop",
                table: "product_variants",
                columns: new[] { "ProductId", "FormatId", "PaperTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_Slug",
                schema: "eshop",
                table: "products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_items",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "product_variants",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "shopping_carts",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "formats",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "paper_types",
                schema: "eshop");

            migrationBuilder.DropTable(
                name: "products",
                schema: "eshop");
        }
    }
}
