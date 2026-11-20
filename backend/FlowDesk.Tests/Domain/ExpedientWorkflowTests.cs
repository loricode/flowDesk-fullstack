using FlowDesk.Domain.Common;
using FlowDesk.Domain.Documents;
using FlowDesk.Domain.Expedients;
using FlowDesk.Domain.Workflow;
using FlowDesk.Shared.Contracts;
using P = FlowDesk.Shared.Contracts.FlowDeskPermissions;

namespace FlowDesk.Tests.Domain;

public class ExpedientWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Operator = Guid.NewGuid();
    private static readonly Guid Validator = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly string[] ValidatorPerms = [P.ExpedientsRead, P.WorkflowValidate];
    private static readonly string[] ApproverPerms = [P.ExpedientsRead, P.WorkflowApprove];

    private readonly WorkflowDefinition _workflow = WorkflowDefinition.CreateDefault(P.WorkflowValidate, P.WorkflowApprove, P.DocumentsUpload);

    private static Expedient NewExpedient() =>
        Expedient.Create("EXP-1", "Solicitud", "Cliente", null, ExpedientPriority.Normal, Operator, null, Now);

    private static Document AddValidatedDocument(Expedient e, string name = "a.pdf")
    {
        var doc = e.AddDocument(name, "application/pdf", $"k/{name}", 100, DocumentSources.DesktopFile, Operator, Now);
        doc.CompleteProcessing(doc.CurrentVersion, Guid.NewGuid().ToString("N"), null);
        return doc;
    }

    [Fact]
    public void Create_starts_received_with_history_and_event()
    {
        var e = NewExpedient();

        Assert.Equal(ExpedientStatus.Received, e.Status);
        Assert.Single(e.History);
        Assert.IsType<ExpedientCreated>(Assert.Single(e.DomainEvents));
    }

    [Fact]
    public void Happy_path_reaches_approved_and_records_approval()
    {
        var e = NewExpedient();
        AddValidatedDocument(e);

        e.ApplyTransition(_workflow.GetTransition("start-validation"), Validator, ValidatorPerms, null, Now);
        e.ApplyTransition(_workflow.GetTransition("send-to-approval"), Validator, ValidatorPerms, null, Now);
        e.ApplyTransition(_workflow.GetTransition("approve"), Approver, ApproverPerms, "OK", Now);

        Assert.Equal(ExpedientStatus.Approved, e.Status);
        Assert.NotNull(e.ClosedAt);
        Assert.Equal(ApprovalDecision.Approved, Assert.Single(e.Approvals).Decision);
        Assert.Equal(4, e.History.Count);
    }

    [Fact]
    public void Transition_from_wrong_state_is_rejected()
    {
        var e = NewExpedient();
        var ex = Assert.Throws<DomainException>(() =>
            e.ApplyTransition(_workflow.GetTransition("approve"), Approver, ApproverPerms, null, Now));
        Assert.Equal("invalid_transition", ex.Code);
    }

    [Fact]
    public void User_without_permission_cannot_transition()
    {
        var e = NewExpedient();
        AddValidatedDocument(e);

        Assert.Throws<DomainForbiddenException>(() =>
            e.ApplyTransition(_workflow.GetTransition("start-validation"), Operator, [P.ExpedientsRead], null, Now));
    }

    [Fact]
    public void Validation_requires_documents()
    {
        var e = NewExpedient();
        var ex = Assert.Throws<DomainException>(() =>
            e.ApplyTransition(_workflow.GetTransition("start-validation"), Validator, ValidatorPerms, null, Now));
        Assert.Equal("documents_required", ex.Code);
    }

    [Fact]
    public void Cannot_send_to_approval_while_documents_are_processing()
    {
        var e = NewExpedient();
        e.AddDocument("a.pdf", "application/pdf", "k", 10, DocumentSources.Web, Operator, Now);
        e.ApplyTransition(_workflow.GetTransition("start-validation"), Validator, ValidatorPerms, null, Now);

        var ex = Assert.Throws<DomainException>(() =>
            e.ApplyTransition(_workflow.GetTransition("send-to-approval"), Validator, ValidatorPerms, null, Now));
        Assert.Equal("documents_processing", ex.Code);
    }

    [Fact]
    public void Reject_requires_comment()
    {
        var e = NewExpedient();
        AddValidatedDocument(e);
        e.ApplyTransition(_workflow.GetTransition("start-validation"), Validator, ValidatorPerms, null, Now);
        e.ApplyTransition(_workflow.GetTransition("send-to-approval"), Validator, ValidatorPerms, null, Now);

        var ex = Assert.Throws<DomainException>(() =>
            e.ApplyTransition(_workflow.GetTransition("reject"), Approver, ApproverPerms, "  ", Now));
        Assert.Equal("comment_required", ex.Code);
    }

    [Fact]
    public void Same_file_name_creates_new_version_instead_of_duplicate()
    {
        var e = NewExpedient();
        e.AddDocument("contrato.pdf", "application/pdf", "k1", 10, DocumentSources.Web, Operator, Now);
        var doc = e.AddDocument("CONTRATO.pdf", "application/pdf", "k2", 20, DocumentSources.Scanner, Operator, Now);

        Assert.Single(e.Documents);
        Assert.Equal(2, doc.CurrentVersion);
        Assert.Equal(DocumentStatus.Pending, doc.Status);
    }

    [Fact]
    public void Closed_expedient_does_not_accept_documents()
    {
        var e = NewExpedient();
        AddValidatedDocument(e);
        e.ApplyTransition(_workflow.GetTransition("start-validation"), Validator, ValidatorPerms, null, Now);
        e.ApplyTransition(_workflow.GetTransition("reject-validation"), Validator, ValidatorPerms, "Incompleto", Now);

        Assert.Throws<DomainException>(() =>
            e.AddDocument("b.pdf", "application/pdf", "k", 10, DocumentSources.Web, Operator, Now));
    }

    [Fact]
    public void Each_change_rotates_concurrency_stamp()
    {
        var e = NewExpedient();
        var before = e.ConcurrencyStamp;
        e.AddDocument("a.pdf", "application/pdf", "k", 10, DocumentSources.Web, Operator, Now);
        Assert.NotEqual(before, e.ConcurrencyStamp);
    }

    [Fact]
    public void Document_processing_is_idempotent()
    {
        var e = NewExpedient();
        var doc = e.AddDocument("a.pdf", "application/pdf", "k", 10, DocumentSources.Web, Operator, Now);

        Assert.True(doc.CompleteProcessing(1, "HASH", null));
        Assert.False(doc.CompleteProcessing(1, "HASH", "otro"));
        Assert.Equal(DocumentStatus.Validated, doc.Status);
    }

    [Fact]
    public void Available_transitions_depend_on_permissions()
    {
        var forValidator = _workflow.AvailableFrom(ExpedientStatus.InValidation, ValidatorPerms).Select(t => t.Code);
        var forApprover = _workflow.AvailableFrom(ExpedientStatus.InValidation, ApproverPerms);

        Assert.Equal(["send-to-approval", "request-correction", "reject-validation"], forValidator);
        Assert.Empty(forApprover);
    }
}
