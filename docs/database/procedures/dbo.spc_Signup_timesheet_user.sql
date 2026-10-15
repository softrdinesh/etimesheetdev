/*
    Procedure: dbo.spc_Signup_timesheet_user
    Recorded:  2026-10-15, as supplied by the database owner.

    Signs a user up to the timesheet and puts them on a project:
      - Email not yet in dbo.Signup: adds the user to dbo.Signup (RoleID 2,
        organisation name and size copied from dbo.Organization), adds their
        dbo.Login row, and adds their dbo.Timesheet_Users_Project_setup row.
      - Email already in dbo.Signup: adds only a
        dbo.Timesheet_Users_Project_setup row for the existing user.
    The email match is case- and space-insensitive.

    Called by:
      src/ETimeSheet.Infrastructure/Repositories/TimesheetProjectRepository.cs
      (POST api/v1/TimesheetProject/create-timesheet-user)
    The API validates every input first - organisation, country, project and
    role must exist, and an existing email must be a live user of the same
    organisation - and rolls back the transaction the CATCH leaves open.

    Recorded as CREATE OR ALTER: the owner supplied it as ALTER PROCEDURE,
    which only works where the procedure already exists. The body below is
    exactly as supplied.

    Quirks worth knowing before calling it:
      - NO RESULT. @Code (5999 success / 6000 failure) and @Message are set but
        never SELECTed or returned through OUTPUT parameters, so a caller sees
        nothing either way - success and failure look the same.
      - The CATCH block does not ROLLBACK. A failure inside the TRY leaves the
        transaction open on the caller's connection.
      - Pwd is HASHBYTES('SHA2_512', @Email) - a hash of the EMAIL, not of a
        password. It is unsalted and derivable by anyone who knows the
        address, so it does not protect the account.
      - @Projectloclongtitude is accepted but never used. ProjectlocLatitude
        is written for a new user only, not in the existing-user branch.
      - Nothing stops the same user being put on the same project twice: the
        existing-user branch always inserts.
      - The email is stored UPPER(TRIM(@Email)) in dbo.Signup.
      - Signup and Login are stamped GETUTCDATE(); the project setup row is
        stamped GETDATE() (server local time).

    The tables are recorded at:
      ../schema/dbo.Timesheet_Users_Project_setup.sql

    NOT RECORDED - dbo.Login and dbo.Organization have never been supplied.
    dbo.Signup is mapped (SignupConfiguration) but its Pwd column is not.

    TEST CONTAINER: the integration fixture replays this folder. Its
    dbo.Signup and dbo.Organization are built from the EF mapping, which has
    neither Signup.Pwd nor Organization.OrganizationSize, so creating this
    procedure there fails with "Invalid column name".
*/

CREATE OR ALTER Procedure [dbo].[spc_Signup_timesheet_user](@Fullname nvarchar(50),@Email nvarchar(100),@CountryID int=null,@Address nvarchar(max),@OrgID int=null,
@ProjectroleID int=null,@Jobtype int=null,@Range int=null,@Costrate decimal(18,2),@Billrate decimal(18,2),@LoginuserID int,@worklocrange int=null,@projectID int=null,
@Projectloclatitude nvarchar(25)=null,@Projectloclongtitude nvarchar(25)=null
)
AS
BEGIN
Declare @Encrypt_pwd binary(64);
Declare @Code Int
Declare @ID int
Declare @Message nvarchar(max)
Declare @OrganizationName nvarchar(50)
Declare @Organizationsize nvarchar(15)
SET @Encrypt_pwd=HASHBYTES('SHA2_512', @Email);
Declare @OrganID int=0;
BEGIN TRAN
BEGIN TRY
Select @OrganizationName=OrganizationName, @Organizationsize =isnull(OrganizationSize,'') from dbo.Organization where OrganizationID=@OrgID
IF not exists(select * from dbo.Signup where TRIM(upper(Email))=TRIM(upper(@Email)))
BEGIN
    Insert into dbo.Signup(Name,Email,Pwd,Accountsource,CreateDate,CountryID,Latitude,Longtitude,OrganizationName,OrganizationSize,Address,OrganizationID,IsDelete,IsProductOwner,RoleID)
  Values(@Fullname,upper(trim(@Email)),@Encrypt_pwd,NULL,GETUTCDATE(),@CountryID,'','',@OrganizationName,@Organizationsize,@Address,@OrgID,0,0,2)
  Select @ID=SCOPE_IDENTITY();
  Insert into dbo.Login(UserID, IsLoginSuccess, LoginErrcnt, LastLogindatetime, CurrentLatitude, CurrentLongtitude) values(@ID,0,0,GETUTCDATE(),'','')
  Insert into dbo.Timesheet_Users_Project_setup(UserID,ProjectRoleID,Cost_rate_per_hour,Billable_rate_per_hour,CreateDate,CreatedBy,IsDelete,Jobtype,Worklocationrange,ProjectID,ProjectlocLatitude)
   values(@ID,@ProjectroleID,@Costrate,@Billrate,GETDATE(),@LoginuserID,0,@Jobtype,@worklocrange,@projectID,@Projectloclatitude)
  SET @Code=5999
  SET @Message='Account created'
END
ELSE
BEGIN
    Select @ID=UserID from dbo.Signup where TRIM(upper(Email))=TRIM(upper(@Email))
    Insert into dbo.Timesheet_Users_Project_setup(UserID,ProjectRoleID,Cost_rate_per_hour,Billable_rate_per_hour,CreateDate,CreatedBy,IsDelete,Jobtype,Worklocationrange,ProjectID)
   values(@ID,@ProjectroleID,@Costrate,@Billrate,GETDATE(),@LoginuserID,0,@Jobtype,@worklocrange,@projectID)
  SET @Code=5999
  SET @Message='Account created'
END
COMMIT TRAN
END TRY
BEGIN CATCH
SET @Code=6000
SET @Message='Account not created.Please send an email to Contactus@appsure.co.in'
END CATCH

END
GO
