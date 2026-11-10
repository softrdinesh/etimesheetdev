using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// The <c>AutoApproveProjectSheet</c> job's settings - who an automatically
/// approved sheet is recorded as approved by.
/// <para>
/// Nobody presses a button for an automatic approval, so
/// <c>dbo.TimesheetProjectSubmission.ApprovedBy</c> and the audit log's
/// <c>CreatedBy</c> need a user to name. That user is configured rather than
/// hard-coded, so it can differ per environment and be changed without a
/// deployment.
/// </para>
/// </summary>
public class AutoApproveSettings
{
    public const string SectionName = "AutoApprove";

    /// <summary>
    /// The <c>dbo.Signup.UserID</c> recorded as the approver of every
    /// automatically approved sheet. Must be greater than 0: a missing or
    /// zero value fails startup rather than writing <c>0</c> as an approver.
    /// <para>
    /// This is not checked against the project's approver setup, and it is not
    /// checked to exist - the job approves on the project's own policy, not on
    /// anyone's behalf, and the id is only what the row records.
    /// </para>
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "AutoApprove:ApprovedByUserId must be a user id greater than 0.")]
    public int ApprovedByUserId { get; set; }
}
