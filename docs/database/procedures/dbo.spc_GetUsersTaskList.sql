/*
    Procedure: dbo.spc_GetUsersTaskList
    Recorded: 2026-09-24, as supplied by the database owner.

    Returns the tasks one user owns, as TWO result sets:
      1. Project tasks - dbo.TaskMaster, keyed TaskID.
      2. Sprint tasks  - dbo.SprintTaskManagement, keyed SprintTaskID and
                         returned under the alias TaskID.
    Both sets have the same two columns, TaskID and Taskname, so one row type
    maps either.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/TimeLogRepository.cs
      (GET /api/v1/TimeLog/get-user-task-list-by-userid/{userId})
    Result sets mapped by:
      src/ETimeSheet.Application/Models/TimeLog.cs (UserTaskListItem)

    NOT a DbSet on Context. EF Core's FromSql reads only the FIRST result set of
    a batch, so the sprint tasks would be silently dropped. The repository runs
    the procedure through the context's own connection and reads each result
    set by column name.

    The SELECT lists are a contract: the repository reads TaskID and Taskname
    by name, so renaming either column, or the TaskID alias on the sprint set,
    breaks the read. So does swapping the ORDER of the two sets - the first is
    always taken as the project tasks.

    NOT RECORDED - the two tables this reads, dbo.TaskMaster and
    dbo.SprintTaskManagement, have never been supplied. The column types are
    therefore unconfirmed: the mapping assumes TaskID / SprintTaskID are int
    and Taskname is a string. Until the tables are recorded, the procedure
    compiles in the integration container (SQL Server defers name resolution)
    but fails when run with "Invalid object name".

    Deployed with ALTER PROCEDURE; recorded as CREATE OR ALTER so that the
    integration fixture can replay it into an empty container.
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetUsersTaskList]
(
    @pUserID INT
)
AS
BEGIN
    SET NOCOUNT ON;

    -- Project Task
    SELECT
        TaskID,
        Taskname
    FROM
        TaskMaster
    WHERE Taskowner = @pUserID;

    -- Sprint Task
    SELECT
        SprintTaskID AS TaskID,
        Taskname
    FROM
        SprintTaskManagement
    WHERE Taskowner = @pUserID;
END;
GO
