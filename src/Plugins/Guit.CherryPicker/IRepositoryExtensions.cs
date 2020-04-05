﻿using System;
using System.IO;
using System.Linq;
using Guit;
using Guit.Plugin.CherryPicker;

namespace LibGit2Sharp
{
    // TODO: Refactor these extension methods into IGitRepository 
    static class IRepositoryExtensions
    {
        public static string GetName(this IRepository repository) => new DirectoryInfo(repository.Info.WorkingDirectory).Name;

        public static Branch GetBranch(this IRepository repository, string branchFriendlyName) =>
            repository.Branches.FirstOrDefault(x => x.FriendlyName == branchFriendlyName);

        static void GetLocalAndRemoteBranch(this IRepository repository, string? localBranchName, string? remoteBranchName, out Branch? localBranch, out Branch? remoteBranch)
        {
            localBranch = localBranchName is null ? default : repository.GetBranch(localBranchName);
            remoteBranch = remoteBranchName is null ? default : repository.GetBranch(remoteBranchName);
        }

        public static Branch? GetBaseBranch(this IRepository repository, CherryPickConfig config)
        {
            repository.GetLocalAndRemoteBranch(config.BaseBranch, config.BaseBranchRemote, out var localBranch, out var remoteBranch);

            return localBranch ?? remoteBranch;
        }

        public static Branch SwitchToTargetBranch(this IGitRepository repository, CherryPickConfig config)
        {
            GetLocalAndRemoteBranch(repository, config.TargetBranch, config.TargetBranchRemote, out var targetBranch, out var targetBranchRemote);

            if (targetBranch == null && targetBranchRemote != null)
                targetBranch = repository.CreateBranch(config.TargetBranch, targetBranchRemote.Tip);

            if (targetBranch is null)
                throw new InvalidOperationException(string.Format("Branch {0} not found", config.TargetBranch));

            // Checkout target branch
            repository.Checkout(targetBranch);

            if (config.SyncTargetBranch && targetBranchRemote != null)
            {
                try
                {
                    // And try pull with fast forward from remote
                    repository.Merge(
                        targetBranchRemote,
                        repository.Config.BuildSignature(DateTimeOffset.Now),
                        new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly });
                }
                catch (NonFastForwardException) { }
            }

            return targetBranch;
        }
    }
}