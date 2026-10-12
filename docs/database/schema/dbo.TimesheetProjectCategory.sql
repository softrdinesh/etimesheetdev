/*
    Table: dbo.TimesheetProjectCategory
    Recorded: 2026-10-12 (sp_help column list supplied by the database owner,
              who created the table by hand)
    Revised:  2026-10-12 - OrganizationID int NOT NULL added by the owner.
    Revised:  2026-10-12 - ID is IDENTITY(1,1) (stated by the owner).

    Lookup of project categories, by name, per organization, with soft-delete
    and audit columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectCategory.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectCategoryConfiguration.cs
      (for the soft delete in POST api/v1/Admin/remove-timesheet-project-category)

    Read by:
      ../procedures/dbo.spc_GetTimesheetProjectCategory.sql
      (AdminRepository, for GET api/v1/Admin/get-timesheet-project-category-by-orgID/{orgID})

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetProjectCategory.sql
      (AdminRepository, for POST api/v1/Admin/add-edit-timesheet-project-category)

    Column quirks worth knowing:
      - ID is IDENTITY(1,1): the database assigns it on insert, and
        spc_AddEditTimesheetProjectCategory reads it back with SCOPE_IDENTITY().
      - Column names are irregularly cased: Categoryname (lower-case "name"),
        IsDelete (not IsDeleted), Deletedby (lower-case "by"), alongside
        CreateDate / CreatedBy / DeletedDate. Map each one by its exact name.
      - Only ID and OrganizationID are NOT NULL. Everything else is nullable
        - including IsDelete, so a row may be neither deleted nor not-deleted.
      - OrganizationID scopes a category to one organization; presumably
        dbo.Organization's id, but no foreign key was reported.
      - CreateDate and DeletedDate are plain datetime - local or UTC is not
        stated.
      - CreatedBy and Deletedby are int user ids; no foreign key was reported.

    STILL UNCONFIRMED:
      - Whether ID is the primary key. It is IDENTITY(1,1), but no key was
        reported, so none is recorded below.
      - Defaults, constraints and indexes - none were supplied.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 100 it prints for Categoryname is 50 characters.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectCategory', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectCategory;
GO

CREATE TABLE dbo.TimesheetProjectCategory
(
    ID            int            IDENTITY(1,1) NOT NULL,
    Categoryname  nvarchar(50)   NULL,
		OrganizationID int           NOT NULL,
    IsDelete      bit            NULL,
    CreateDate    datetime       NULL,
    CreatedBy     int            NULL,
    DeletedDate   datetime       NULL,
    Deletedby     int            NULL
);
GO
