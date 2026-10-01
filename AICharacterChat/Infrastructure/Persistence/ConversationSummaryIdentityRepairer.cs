using System;
using System.Collections.Generic;
using AICharacterChat.Domain.Models;

namespace AICharacterChat.Infrastructure.Persistence
{
    internal static class ConversationSummaryIdentityRepairer
    {
        public static bool Repair(WorldStore store)
        {
            var changed = false;

            foreach (var world in store.Worlds)
            {
                foreach (var session in world.ChatSessions)
                    changed |= RepairSession(session);
            }

            return changed;
        }

        private static bool RepairSession(ChatSession session)
        {
            var changed = false;
            var usedIds = new HashSet<Guid>();

            foreach (var summary in session.Summaries)
            {
                if (summary.Id != Guid.Empty && usedIds.Add(summary.Id))
                    continue;

                summary.Id = CreateUniqueId(usedIds);
                usedIds.Add(summary.Id);
                changed = true;
            }

            return changed;
        }

        private static Guid CreateUniqueId(HashSet<Guid> usedIds)
        {
            Guid id;
            do
            {
                id = Guid.NewGuid();
            }
            while (id == Guid.Empty || usedIds.Contains(id));

            return id;
        }
    }
}
