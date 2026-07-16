using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestrictPoint.Api.Licensing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLicensingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Licensing");

            migrationBuilder.CreateTable(
                name: "Installations",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WebPartGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SdkVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InstalledUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastValidatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Licenses",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeveloperOrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IssuedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenseTokens",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    KeyId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IssuedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Revoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LicenseFeatures",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseFeatures_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "Licensing",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LicenseLimits",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LimitKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseLimits_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "Licensing",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LicenseWebParts",
                schema: "Licensing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebPartGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseWebParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LicenseWebParts_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "Licensing",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Installations_LicenseId_InstallationId",
                schema: "Licensing",
                table: "Installations",
                columns: new[] { "LicenseId", "InstallationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseFeatures_LicenseId_FeatureKey",
                schema: "Licensing",
                table: "LicenseFeatures",
                columns: new[] { "LicenseId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseLimits_LicenseId_LimitKey",
                schema: "Licensing",
                table: "LicenseLimits",
                columns: new[] { "LicenseId", "LimitKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_CustomerTenantId_ProjectId",
                schema: "Licensing",
                table: "Licenses",
                columns: new[] { "CustomerTenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_DeveloperOrganizationId",
                schema: "Licensing",
                table: "Licenses",
                column: "DeveloperOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_ProjectId",
                schema: "Licensing",
                table: "Licenses",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseTokens_LicenseId",
                schema: "Licensing",
                table: "LicenseTokens",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseTokens_TokenId",
                schema: "Licensing",
                table: "LicenseTokens",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LicenseWebParts_LicenseId_WebPartGuid",
                schema: "Licensing",
                table: "LicenseWebParts",
                columns: new[] { "LicenseId", "WebPartGuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DispatchedUtc",
                schema: "Licensing",
                table: "OutboxMessages",
                column: "DispatchedUtc",
                filter: "[DispatchedUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Installations",
                schema: "Licensing");

            migrationBuilder.DropTable(
                name: "LicenseFeatures",
                schema: "Licensing");

            migrationBuilder.DropTable(
                name: "LicenseLimits",
                schema: "Licensing");

            migrationBuilder.DropTable(
                name: "LicenseTokens",
                schema: "Licensing");

            migrationBuilder.DropTable(
                name: "LicenseWebParts",
                schema: "Licensing");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "Licensing");

            migrationBuilder.DropTable(
                name: "Licenses",
                schema: "Licensing");
        }
    }
}
