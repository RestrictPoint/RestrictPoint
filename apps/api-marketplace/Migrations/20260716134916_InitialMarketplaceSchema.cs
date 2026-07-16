using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestrictPoint.Api.Marketplace.Migrations
{
    /// <inheritdoc />
    public partial class InitialMarketplaceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marketplace");

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Listings",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebPartGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    InstallCount = table.Column<int>(type: "int", nullable: false),
                    AverageRating = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    ReviewCount = table.Column<int>(type: "int", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Screenshots = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupportUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingPlans",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PricingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BillingInterval = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StripePriceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LicenseTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingPlans_Listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    EditedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingTags",
                schema: "marketplace",
                columns: table => new
                {
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingTags", x => new { x.ListingId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ListingTags_Listings_ListingId",
                        column: x => x.ListingId,
                        principalSchema: "marketplace",
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListingTags_Tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "marketplace",
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DisplayOrder",
                schema: "marketplace",
                table: "Categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                schema: "marketplace",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                schema: "marketplace",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CategoryId",
                schema: "marketplace",
                table: "Listings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_IsFeatured",
                schema: "marketplace",
                table: "Listings",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_OrganizationId",
                schema: "marketplace",
                table: "Listings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_ProjectId",
                schema: "marketplace",
                table: "Listings",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status",
                schema: "marketplace",
                table: "Listings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_IsFeatured_AverageRating",
                schema: "marketplace",
                table: "Listings",
                columns: new[] { "Status", "IsFeatured", "AverageRating" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingTags_TagId",
                schema: "marketplace",
                table: "ListingTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DispatchedUtc",
                schema: "marketplace",
                table: "OutboxMessages",
                column: "DispatchedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DispatchedUtc_AttemptCount",
                schema: "marketplace",
                table: "OutboxMessages",
                columns: new[] { "DispatchedUtc", "AttemptCount" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_IsActive",
                schema: "marketplace",
                table: "PricingPlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_ListingId",
                schema: "marketplace",
                table: "PricingPlans",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_StripePriceId",
                schema: "marketplace",
                table: "PricingPlans",
                column: "StripePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId",
                schema: "marketplace",
                table: "Reviews",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ListingId_UserId",
                schema: "marketplace",
                table: "Reviews",
                columns: new[] { "ListingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Rating",
                schema: "marketplace",
                table: "Reviews",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                schema: "marketplace",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                schema: "marketplace",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Slug",
                schema: "marketplace",
                table: "Tags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UsageCount",
                schema: "marketplace",
                table: "Tags",
                column: "UsageCount");

            // Predefined hierarchical taxonomy (docs/13). RowVersion is store-generated.
            migrationBuilder.InsertData(
                schema: "marketplace",
                table: "Categories",
                columns: ["Id", "Name", "ParentCategoryId", "Slug", "DisplayOrder", "CreatedUtc", "UpdatedUtc", "DeletedUtc"],
                values: new object[,]
                {
                    { new Guid("a1f60d6f-0001-4a10-9c60-000000000001"), "Productivity", null, "productivity", 1, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                    { new Guid("a1f60d6f-0002-4a10-9c60-000000000002"), "Analytics", null, "analytics", 2, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                    { new Guid("a1f60d6f-0003-4a10-9c60-000000000003"), "CRM Integration", null, "crm-integration", 3, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                    { new Guid("a1f60d6f-0004-4a10-9c60-000000000004"), "Security", null, "security", 4, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                    { new Guid("a1f60d6f-0005-4a10-9c60-000000000005"), "Automation", null, "automation", 5, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                    { new Guid("a1f60d6f-0006-4a10-9c60-000000000006"), "AI Tools", null, "ai-tools", 6, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                    { new Guid("a1f60d6f-0007-4a10-9c60-000000000007"), "Data Visualization", null, "data-visualization", 7, new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero), null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categories",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "ListingTags",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "PricingPlans",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "Tags",
                schema: "marketplace");

            migrationBuilder.DropTable(
                name: "Listings",
                schema: "marketplace");
        }
    }
}
