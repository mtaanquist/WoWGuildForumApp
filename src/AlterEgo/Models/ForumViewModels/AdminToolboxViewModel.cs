using System.Collections.Generic;

namespace AlterEgo.Models.ForumViewModels
{
    public sealed class AdminToolboxViewModel
    {
        // Meta
        public List<Category> Categories { get; set; }

        // New category
        public string CategoryName { get; set; }
        public int CategoryReadableBy { get; set; }

        // New forum
        public string ForumName { get; set; }
        public string ForumDescription { get; set; }
        public int CategoryId { get; set; }
        public int ForumReadableBy { get; set; } = (int)GuildRank.Everyone;
        public int ForumCanStartThreads { get; set; } = (int)GuildRank.Triallist;
        public int ForumCanReplyToThreads { get; set; } = (int)GuildRank.Everyone;
        public int ForumCanLockThreads { get; set; } = (int)GuildRank.ForumAdmin;
        public int ForumCanStickyThreads { get; set; } = (int)GuildRank.ForumAdmin;
        public int ForumCanEditThreads { get; set; } = (int)GuildRank.ForumAdmin;
        public int ForumCanDeleteThreads { get; set; } = (int)GuildRank.ForumAdmin;
    }
}
