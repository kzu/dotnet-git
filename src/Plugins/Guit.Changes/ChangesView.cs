﻿using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Guit.Events;
using Guit.Plugin.Changes.Properties;
using LibGit2Sharp;
using Merq;

namespace Guit.Plugin.Changes
{
    [Shared]
    [Export]
    [Export(typeof(IChangesView))]
    [ContentView(WellKnownViews.Changes, '1', resources: typeof(Resources))]
    public class ChangesView : ContentView, IChangesView
    {
        readonly IRepository repository;
        readonly IEventStream eventStream;

        ListView<FileStatus> view;

        [ImportingConstructor]
        public ChangesView(IRepository repository, IEventStream eventStream)
            : base("Changes")
        {
            this.repository = repository;
            this.eventStream = eventStream;

            var status = repository.RetrieveStatus(new StatusOptions());

            view = new ListView<FileStatus>()
            {
                AllowsMarking = true
            };
            view.SelectedChanged += OnSelectedChanged;

            Content = view;
        }

        public override void Refresh()
        {
            base.Refresh();

            var status = repository.RetrieveStatus(new StatusOptions());
            var files = status
                .Where(x => x.State != LibGit2Sharp.FileStatus.Ignored)
                .Select(x => new FileStatus(x))
                .OrderByDescending(x => IsSubmodule(x.Entry.FilePath))
                .ThenBy(x => x.Status)
                .ThenBy(x => x.Entry.FilePath)
                .ToList();

            view.SetValues(files);

            if (files.Count > 0)
                OnSelectedChanged();
        }

        bool IsSubmodule(string filepath) =>
            repository.Submodules.Any(x => x.Path == filepath);

        void OnSelectedChanged() =>
            eventStream.Push<SelectionChanged>(view.Values.ElementAt(view.SelectedItem).Entry.FilePath);

        public virtual IEnumerable<StatusEntry> GetMarkedEntries(bool? submoduleEntriesOnly = null) => view.Values
            .Where((x, i) => view.Source.IsMarked(i) &&
                (submoduleEntriesOnly == null || IsSubmodule(x.Entry.FilePath) == submoduleEntriesOnly))
            .Select(x => x.Entry);

        class FileStatus
        {
            public FileStatus(StatusEntry entry)
            {
                Entry = entry;

                switch (entry.State)
                {
                    case LibGit2Sharp.FileStatus.Conflicted:
                        Status = Status.Conflicted;
                        break;
                    case LibGit2Sharp.FileStatus.DeletedFromIndex:
                    case LibGit2Sharp.FileStatus.DeletedFromWorkdir:
                        Status = Status.Deleted;
                        break;
                    case LibGit2Sharp.FileStatus.ModifiedInIndex:
                    case LibGit2Sharp.FileStatus.ModifiedInWorkdir:
                        Status = Status.Modified;
                        break;
                    case LibGit2Sharp.FileStatus.NewInIndex:
                    case LibGit2Sharp.FileStatus.NewInWorkdir:
                        Status = Status.Added;
                        break;
                    case LibGit2Sharp.FileStatus.RenamedInIndex:
                    case LibGit2Sharp.FileStatus.RenamedInWorkdir:
                        Status = Status.Renamed;
                        break;
                }
            }

            public StatusEntry Entry { get; }

            public Status Status { get; }

            public override string ToString()
            {
                switch (Status)
                {
                    case Status.Added:
                        return "+ " + Entry.FilePath;
                    case Status.Deleted:
                        return "- " + Entry.FilePath;
                    case Status.Modified:
                        return "* " + Entry.FilePath;
                    case Status.Conflicted:
                        return "! " + Entry.FilePath;
                    case Status.Renamed:
                        return "~ " + Entry.FilePath;
                    default:
                        return "+ " + Entry.FilePath;
                }
            }

            public override int GetHashCode() => Entry.GetHashCode();

            public override bool Equals(object obj) => Entry.Equals((obj as FileStatus)?.Entry);

        }

        enum Status
        {
            Conflicted,
            Added,
            Modified,
            Deleted,
            Renamed
        }
    }
}