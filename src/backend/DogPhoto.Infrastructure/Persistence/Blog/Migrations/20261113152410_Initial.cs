using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DogPhoto.Infrastructure.Persistence.Blog.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "blog");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "blog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    NameSk = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                schema: "blog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TitleSk = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Slug = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExcerptSk = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExcerptEn = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ContentMarkdownSk = table.Column<string>(type: "text", nullable: true),
                    ContentMarkdownEn = table.Column<string>(type: "text", nullable: true),
                    ContentHtmlSk = table.Column<string>(type: "text", nullable: true),
                    ContentHtmlEn = table.Column<string>(type: "text", nullable: true),
                    FeaturedImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Author = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetaTitleSk = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MetaTitleEn = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MetaDescriptionSk = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MetaDescriptionEn = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "blog",
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
                name: "post_categories",
                schema: "blog",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_categories", x => new { x.PostId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_post_categories_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "blog",
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_categories_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "blog",
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_tags",
                schema: "blog",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_tags", x => new { x.PostId, x.TagId });
                    table.ForeignKey(
                        name: "FK_post_tags_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "blog",
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_tags_tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "blog",
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_Slug",
                schema: "blog",
                table: "categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_categories_CategoryId",
                schema: "blog",
                table: "post_categories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_post_tags_TagId",
                schema: "blog",
                table: "post_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_Slug",
                schema: "blog",
                table: "posts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_Slug",
                schema: "blog",
                table: "tags",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_categories",
                schema: "blog");

            migrationBuilder.DropTable(
                name: "post_tags",
                schema: "blog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "blog");

            migrationBuilder.DropTable(
                name: "posts",
                schema: "blog");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "blog");
        }
    }
}
