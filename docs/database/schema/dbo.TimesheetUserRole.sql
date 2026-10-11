/*
    Table: dbo.TimesheetUserRole
    Recorded: 2026-10-11 (sp_help column list supplied by the database owner;
              the table already exists)
    Revised:  2026-10-11 - RoleID is IDENTITY(1,1) (per the owner).

    The roles defined for an organisation - one row per role, per
    organisation.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetUserRole.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetUserRoleConfiguration.cs

    Read by:
      ../procedures/dbo.spc_GetUserRoleListByOrgID.sql
      (AdminRepository, for GET api/v1/Admin/get-user-role-list-by-orgid/{orgID})
      src/ETimeSheet.Infrastructure/Repositories/AdminRepository.cs
      (UserRoleNameExistsAsync - the duplicate-name check before a save)

    Written by:
      ../procedures/dbo.spc_AddEditUserRoleByAdmin.sql
      (AdminRepository, for POST api/v1/Admin/add-edit-user-role)
      src/ETimeSheet.Infrastructure/Repositories/AdminRepository.cs
      (UpdateUserRoleAsync - the soft delete for POST api/v1/Admin/delete-user-role:
      IsDelete = 1, Deleteddate, Deletedby)

    Column quirks worth knowing:
      - Column names are inconsistently cased: Rolename, Createdby,
        Deleteddate, Deletedby. Map each one by its exact name.
      - IsDelete is a NULLABLE bit. A row is live when IsDelete is 0 OR NULL;
        only 1 means deleted - the same rule as
        dbo.TimesheetMasterSetup.IsDelete.
      - OrganizationID is nullable, so a role can belong to no organisation;
        such a role is never returned by the by-organisation list.
      - CreateDate and Deleteddate are plain datetime - local or UTC is not
        stated.

    STILL UNCONFIRMED:
      - Whether RoleID is the primary key. It is IDENTITY(1,1), but no key
        was reported, so none is recorded below.
      - Defaults, constraints and indexes - none were supplied.
      - How RoleID relates to dbo.Signup.RoleID and to the API's RoleType enum.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 100 it prints for Rolename is 50 characters.
*/

IF OBJECT_ID(N'dbo.TimesheetUserRole', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetUserRole;
GO

CREATE TABLE dbo.TimesheetUserRole
(
    RoleID          int            IDENTITY(1,1) NOT NULL,
    Rolename        nvarchar(50)   NULL,
    OrganizationID  int            NULL,
    CreateDate      datetime       NULL,
    Createdby       int            NULL,
    IsDelete        bit            NULL,
    Deleteddate     datetime       NULL,
    Deletedby       int            NULL
);
GO
