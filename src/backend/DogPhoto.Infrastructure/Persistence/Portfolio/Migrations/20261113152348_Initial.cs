using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DogPhoto.Infrastructure.Persistence.Portfolio.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "portfolio");

            migrationBuilder.CreateTable(
                name: "collections",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NameSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    CoverPhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "photos",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TitleEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DescriptionSk = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    AltTextSk = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AltTextEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CameraSettings = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    DominantColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Blurhash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OriginalBlobUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NameSk = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "collection_photos",
                schema: "portfolio",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_photos", x => new { x.CollectionId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_collection_photos_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "portfolio",
                        principalTable: "collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collection_photos_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "portfolio",
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_variants",
                schema: "portfolio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photo_variants_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "portfolio",
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "photo_tags",
                schema: "portfolio",
                columns: table => new
                {
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_tags", x => new { x.PhotoId, x.TagId });
                    table.ForeignKey(
                        name: "FK_photo_tags_photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "portfolio",
                        principalTable: "photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_photo_tags_tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "portfolio",
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_photos_PhotoId",
                schema: "portfolio",
                table: "collection_photos",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_collections_Slug",
                schema: "portfolio",
                table: "collections",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_photo_tags_TagId",
                schema: "portfolio",
                table: "photo_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_photo_variants_PhotoId",
                schema: "portfolio",
                table: "photo_variants",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_photos_Slug",
                schema: "portfolio",
                table: "photos",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_Slug",
                schema: "portfolio",
                table: "tags",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_photos",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "photo_tags",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "photo_variants",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "collections",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "portfolio");

            migrationBuilder.DropTable(
                name: "photos",
                schema: "portfolio");
        }
    }
}
