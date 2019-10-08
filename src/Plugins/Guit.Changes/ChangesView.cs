﻿using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Guit.Events;
using LibGit2Sharp;
using Merq;
using Terminal.Gui;

namespace Guit.Plugin.Changes
{
    [Shared]
    [Export]
    [ContentView(nameof(Changes), '1')]
    public class ChangesView : ContentView
    {
        readonly IRepository repository;
        readonly IEventStream eventStream;

        List<FileStatus> files = new List<FileStatus>();
        ListView view;

        [ImportingConstructor]
        public ChangesView(IRepository repository, IEventStream eventStream)
            : base("Changes")
        {
            this.repository = repository;
            this.eventStream = eventStream;

            var status = repository.RetrieveStatus(new StatusOptions());

            view = new ListView(files)
            {
                AllowsMarking = true
            };
            view.SelectedChanged += OnSelectedChanged;

            Content = view;
        }

        public override void Refresh()
        {
            var status = repository.RetrieveStatus(new StatusOptions());
            files = status
                .Added.Concat(status.Untracked).Select(x => new FileStatus(x, Status.Added))
                .Concat(status.Removed.Concat(status.Missing).Select(x => new FileStatus(x, Status.Deleted)))
                .Concat(status.Modified.Select(x => new FileStatus(x, Status.Modified)))
                .OrderByDescending(x => IsSubmodule(x.Entry.FilePath))
                .ThenBy(x => x.Status)
                .ThenBy(x => x.Entry.FilePath)
                .ToList();

            view.SetSource(files);

            // Mark modified files by default
            foreach (var file in files.Where(x => x.Status == Status.Modified))
                view.Source.SetMark(files.IndexOf(file), true);
        }

        bool IsSubmodule(string filepath) =>
            repository.Submodules.Any(x => x.Path == filepath);

        void OnSelectedChanged()
        {
            eventStream.Push<SelectionChanged>(files[view.SelectedItem].Entry);
        }

        public IEnumerable<StatusEntry> GetMarkedEntries(bool? submoduleEntriesOnly = null) => files
            .Where(x => view.Source.IsMarked(files.IndexOf(x)) &&
                (submoduleEntriesOnly == null || IsSubmodule(x.Entry.FilePath) == submoduleEntriesOnly))
            .Select(x => x.Entry);

        class FileStatus
        {
            public FileStatus(StatusEntry entry, Status status)
            {
                Entry = entry;
                Status = status;
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
                    default:
                        return "+ " + Entry.FilePath;
                }
            }
        }

        enum Status
        {
            Modified,
            Added,
            Deleted
        }
    }
}