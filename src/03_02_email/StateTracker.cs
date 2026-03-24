using System.Collections.Generic;
using System.Linq;
using FourthDevs.Email.Data;
using FourthDevs.Email.Knowledge;
using FourthDevs.Email.Models;

namespace FourthDevs.Email
{
    /// <summary>
    /// Tracks state changes (labels added/removed, labels created, drafts created)
    /// and knowledge base accesses across triage and draft turns.
    /// </summary>
    public class StateTracker
    {
        private class SnapshotData
        {
            public List<EmailSnapshot> Emails { get; set; }
            public HashSet<string> LabelIds { get; set; }
            public List<Draft> Drafts { get; set; }
        }

        private readonly SnapshotData _initial;
        private SnapshotData _current;
        private readonly List<Change> _allChanges = new List<Change>();
        private int _kbLogCursor;

        public StateTracker()
        {
            _initial = TakeSnapshot();
            _current = TakeSnapshot();
            _kbLogCursor = 0;
        }

        public void TakeSnapshotForTurn()
        {
            _current = TakeSnapshot();
            _kbLogCursor = AccessLog.Log.Count;
        }

        public List<Change> CollectChanges()
        {
            var after = TakeSnapshot();
            var changes = Diff(_current, after);
            _allChanges.AddRange(changes);
            _current = after;
            return changes;
        }

        public List<KnowledgeAccess> CollectKnowledgeAccess()
        {
            var newEntries = AccessLog.Log.Skip(_kbLogCursor).ToList();
            _kbLogCursor = AccessLog.Log.Count;
            return newEntries;
        }

        public List<Change> AllChanges() => new List<Change>(_allChanges);
        public List<KnowledgeAccess> AllKnowledgeAccess() => new List<KnowledgeAccess>(AccessLog.Log);
        public List<EmailSnapshot> InitialEmails() => _initial.Emails;

        public List<Models.Email> GetEmails() => MockInbox.Emails;
        public List<Label> GetLabels() => MockInbox.Labels;
        public List<Draft> GetDrafts() => MockInbox.Drafts;
        public List<Account> GetAccounts() => MockInbox.Accounts;

        // ── Private helpers ─────────────────────────────────────────

        private static SnapshotData TakeSnapshot()
        {
            return new SnapshotData
            {
                Emails = MockInbox.Emails.Select(e => new EmailSnapshot
                {
                    Id = e.Id,
                    Account = e.Account,
                    From = e.From,
                    Subject = e.Subject,
                    LabelIds = new List<string>(e.LabelIds),
                    IsRead = e.IsRead,
                }).ToList(),
                LabelIds = new HashSet<string>(MockInbox.Labels.Select(l => l.Id)),
                Drafts = new List<Draft>(MockInbox.Drafts),
            };
        }

        private static Label ResolveLabel(string id)
        {
            return MockInbox.Labels.FirstOrDefault(l => l.Id == id);
        }

        private static List<Change> Diff(SnapshotData before, SnapshotData after)
        {
            var changes = new List<Change>();

            foreach (var afterEmail in after.Emails)
            {
                var beforeEmail = before.Emails.FirstOrDefault(e => e.Id == afterEmail.Id);
                if (beforeEmail == null) continue;

                var added = afterEmail.LabelIds.Where(l => !beforeEmail.LabelIds.Contains(l)).ToList();
                var removed = beforeEmail.LabelIds.Where(l => !afterEmail.LabelIds.Contains(l)).ToList();

                foreach (var labelId in added)
                {
                    var label = ResolveLabel(labelId);
                    changes.Add(new Change
                    {
                        Type = "label_added",
                        Account = afterEmail.Account,
                        EmailId = afterEmail.Id,
                        EmailSubject = afterEmail.Subject,
                        LabelName = label != null ? label.Name : labelId,
                        LabelColor = label?.Color,
                    });
                }

                foreach (var labelId in removed)
                {
                    var label = ResolveLabel(labelId);
                    changes.Add(new Change
                    {
                        Type = "label_removed",
                        Account = afterEmail.Account,
                        EmailId = afterEmail.Id,
                        EmailSubject = afterEmail.Subject,
                        LabelName = label != null ? label.Name : labelId,
                    });
                }
            }

            foreach (var labelId in after.LabelIds)
            {
                if (!before.LabelIds.Contains(labelId))
                {
                    var label = ResolveLabel(labelId);
                    if (label != null)
                    {
                        changes.Add(new Change
                        {
                            Type = "label_created",
                            Account = label.Account,
                            LabelName = label.Name,
                            LabelColor = label.Color,
                        });
                    }
                }
            }

            int newDraftCount = after.Drafts.Count - before.Drafts.Count;
            if (newDraftCount > 0)
            {
                var newDrafts = after.Drafts.Skip(before.Drafts.Count);
                foreach (var draft in newDrafts)
                {
                    changes.Add(new Change
                    {
                        Type = "draft_created",
                        Account = draft.Account,
                        DraftTo = draft.To,
                        DraftSubject = draft.Subject,
                    });
                }
            }

            return changes;
        }
    }
}
