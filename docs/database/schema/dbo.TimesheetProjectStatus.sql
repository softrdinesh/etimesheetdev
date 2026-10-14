/*
    Table: dbo.TimesheetProjectStatus
    Recorded: 2026-10-13 (definition supplied by the database owner, who
              creates the table by hand)
    Revised:  2026-10-14 - no longer a shared lookup. The owner added
              Colorcode, OrgID and the audit columns (CreateDate, CreatedBy,
              DeletedDate, Deletedby, UpdatedDate, Updatedby). Rows are now
              per organization, so the fixed Open / Closed / Pending rows
              are no longer inserted here.

    Project statuses, per organization, with a display colour, soft-delete
    and audit columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectStatus.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectStatusConfiguration.cs
      (for the soft delete in POST api/v1/TimesheetProject/remove-timesheet-project-status)

    Read by:
      ../procedures/dbo.spc_GetTimesheetProject.sql and
      ../procedures/dbo.spc_AddEditTimesheetProject.sql - LEFT JOINed on
      dbo.TimesheetProject.Status for StatusName.
      ../procedures/dbo.spc_GetTimesheetProjectStatus.sql - one
      organisation's live statuses, for
      GET api/v1/TimesheetProject/get-timesheet-project-status-by-orgID/{orgID}.

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetProjectStatus.sql
      (TimesheetProjectRepository, for
      POST api/v1/TimesheetProject/add-edit-timesheet-project-status)

    Column quirks worth knowing:
      - StatusID is IDENTITY(1,1) (as originally supplied; the revised sp_help
        column list does not report identity either way).
      - Column names are irregularly cased: Colorcode, Deletedby, Updatedby
        (lower-case "by") alongside CreatedBy. Map each one by its exact name.
      - Only StatusID and Status are NOT NULL - including OrgID, so a row may
        belong to no organization.
      - OrgID presumably dbo.Organization's id; no foreign key was reported.
      - There is no IsDelete flag; DeletedDate / Deletedby are the only
        soft-delete marker.
      - CreateDate, DeletedDate and UpdatedDate are plain datetime - local or
        UTC is not stated.
      - CreatedBy, Deletedby and Updatedby are int user ids; no foreign key
        was reported.
      - dbo.TimesheetProject.Status is presumably a StatusID from this table;
        no foreign key was reported.

    STILL UNCONFIRMED:
      - Whether StatusID is the primary key. No key was reported, so none is
        recorded below.
      - Whether Status is unique per OrgID - no constraint was supplied.
      - Defaults, constraints and indexes - none were supplied.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 40 it prints for Status is 20 characters and the 50 for Colorcode is 25.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectStatus', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectStatus;
GO

CREATE TABLE dbo.TimesheetProjectStatus
(
    StatusID       int             IDENTITY(1,1) NOT NULL,
    Status         nvarchar(20)    NOT NULL,
    Colorcode      nvarchar(25)    NULL,
    CreateDate     datetime        NULL,
    CreatedBy      int             NULL,
    DeletedDate    datetime        NULL,
    Deletedby      int             NULL,
    UpdatedDate    datetime        NULL,
    Updatedby      int             NULL,
    OrgID          int             NULL
);
GO
