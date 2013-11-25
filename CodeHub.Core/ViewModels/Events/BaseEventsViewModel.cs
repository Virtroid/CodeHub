using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Cirrious.MvvmCross.ViewModels;
using CodeFramework.Core.ViewModels;
using CodeHub.Core.Utils;
using CodeHub.Core.ViewModels.Gists;
using CodeHub.Core.ViewModels.Issues;
using CodeHub.Core.ViewModels.PullRequests;
using CodeHub.Core.ViewModels.Repositories;
using CodeHub.Core.ViewModels.Source;
using CodeHub.Core.ViewModels.User;
using GitHubSharp;
using GitHubSharp.Models;

namespace CodeHub.Core.ViewModels.Events
{
    public abstract class BaseEventsViewModel : LoadableViewModel
    {
        private readonly CollectionViewModel<Tuple<EventModel, EventBlock>> _events = new CollectionViewModel<Tuple<EventModel, EventBlock>>();

        public CollectionViewModel<Tuple<EventModel, EventBlock>> Events
        {
            get { return _events; }
        }

        public bool ReportRepository
        {
            get;
            private set;
        }

        protected BaseEventsViewModel()
        {
            ReportRepository = true;
        }

        protected override Task Load(bool forceDataRefresh)
        {
            return Task.Run(() => this.RequestModel(CreateRequest(0, 100), forceDataRefresh, response => {
                Events.Items.Reset(CreateDataFromLoad(response.Data));
                this.CreateMore(response, m => Events.MoreItems = m, d => Events.Items.AddRange(CreateDataFromLoad(d)));
            }));
        }

        private IEnumerable<Tuple<EventModel, EventBlock>> CreateDataFromLoad(IEnumerable<EventModel> events)
        {
			return events.Select(x => new Tuple<EventModel, EventBlock>(x, CreateEventTextBlocks(x)));
        }
        
        protected abstract GitHubRequest<List<EventModel>> CreateRequest(int page, int perPage);

        public ICommand GoToCommitsCommand
        {
            get { return new MvxCommand<EventModel>(GoToCommits, x => x.Repo != null); }
        }

        private void GoToCommits(EventModel eventModel)
        {
            var repoId = new RepositoryIdentifier(eventModel.Repo.Name);
            ShowViewModel<ChangesetsViewModel>(new ChangesetsViewModel.NavObject
            {
                Username = repoId.Name,
                Repository = repoId.Owner,
                Branch = ((EventModel.CommitCommentEvent) eventModel.PayloadObject).Comment.CommitId
            });
        }

        public ICommand GoToRepositoryCommand
        {
            get { return new MvxCommand<EventModel.RepoModel>(GoToRepository, x => x != null); }
        }

        private void GoToRepository(EventModel.RepoModel eventModel)
        {
            var repoId = new RepositoryIdentifier(eventModel.Name);
            ShowViewModel<RepositoryViewModel>(new RepositoryViewModel.NavObject
            {
                Username = repoId.Name,
                Repository = repoId.Owner
            });
        }

        private void GoToUser(string username)
        {
            if (string.IsNullOrEmpty(username))
                return;
            ShowViewModel<ProfileViewModel>(new ProfileViewModel.NavObject {Username = username});
        }

        private void GoToBranches(RepositoryIdentifier repoId)
        {
			ShowViewModel<BranchesAndTagsViewModel>(new BranchesAndTagsViewModel.NavObject
            {
                Username = repoId.Name,
				Repository = repoId.Owner,
				IsShowingBranches = true
            });
        }

        public ICommand GoToTagsCommand
        {
            get { return new MvxCommand<EventModel.RepoModel>(GoToTags, x => x != null); }
        }

        private void GoToTags(EventModel.RepoModel eventModel)
        {
            var repoId = new RepositoryIdentifier(eventModel.Name);
			ShowViewModel<BranchesAndTagsViewModel>(new BranchesAndTagsViewModel.NavObject
            {
                Username = repoId.Name,
				Repository = repoId.Owner,
				IsShowingBranches = false
            });
        }

        public ICommand GoToGistCommand
        {
            get { return new MvxCommand<EventModel.GistEvent>(x => ShowViewModel<GistViewModel>(new GistViewModel.NavObject { Id = x.Gist.Id }), x => x != null && x.Gist != null); }
        }

        private void GoToIssue(RepositoryIdentifier repo, ulong id)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Name) || string.IsNullOrEmpty(repo.Owner))
                return;
            ShowViewModel<IssueViewModel>(new IssueViewModel.NavObject
            {
                Username = repo.Owner,
                Repository = repo.Name,
                Id = id
            });
        }

        private void GoToPullRequest(RepositoryIdentifier repo, ulong id)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Name) || string.IsNullOrEmpty(repo.Owner))
                return;
            ShowViewModel<PullRequestViewModel>(new PullRequestViewModel.NavObject
            {
                Username = repo.Owner,
                Repository = repo.Name,
                Id = id
            });
        }

        private void GoToPullRequests(RepositoryIdentifier repo)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Name) || string.IsNullOrEmpty(repo.Owner))
                return;
            ShowViewModel<PullRequestsViewModel>(new PullRequestsViewModel.NavObject
            {
                Username = repo.Owner,
                Repository = repo.Name
            });
        }

        private void GoToChangeset(RepositoryIdentifier repo, string sha)
        {
            if (repo == null || string.IsNullOrEmpty(repo.Name) || string.IsNullOrEmpty(repo.Owner))
                return;
            ShowViewModel<ChangesetsViewModel>(new ChangesetsViewModel.NavObject
            {
                Username = repo.Owner,
                Repository = repo.Name,
                Branch = sha
            });
        }

        private EventBlock CreateEventTextBlocks(EventModel eventModel)
        {
            var eventBlock = new EventBlock();
            var repoId = eventModel.Repo != null ? new RepositoryIdentifier(eventModel.Repo.Name) : new RepositoryIdentifier();
            var username = eventModel.Actor != null ? eventModel.Actor.Login : null;

            // Insert the actor
			eventBlock.Header.Add(new AnchorBlock(username, () => GoToUser(username)));

            var commitCommentEvent = eventModel.PayloadObject as EventModel.CommitCommentEvent;
            if (commitCommentEvent != null)
            {
                var node = commitCommentEvent.Comment.CommitId.Substring(0, commitCommentEvent.Comment.CommitId.Length > 10 ? 10 : commitCommentEvent.Comment.CommitId.Length);
                eventBlock.Tapped = () => GoToCommitsCommand.Execute(eventModel);
                eventBlock.Header.Add(new TextBlock(" commented on commit "));
                eventBlock.Header.Add(new AnchorBlock(node, eventBlock.Tapped));

                if (ReportRepository)
                {
                    eventBlock.Header.Add(new TextBlock(" in "));
                    eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                }

                eventBlock.Body.Add(new TextBlock(commitCommentEvent.Comment.Body));
                return eventBlock;
            }

            var createEvent = eventModel.PayloadObject as EventModel.CreateEvent;
            if (createEvent != null)
            {
                if (createEvent.RefType.Equals("repository"))
                {
                    if (ReportRepository)
                    {
                        eventBlock.Tapped = () => GoToRepositoryCommand.Execute(eventModel.Repo);
						eventBlock.Header.Add(new TextBlock(" created repository "));
                        eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                    }
                    else
						eventBlock.Header.Add(new TextBlock(" created this repository"));
                }
                else if (createEvent.RefType.Equals("branch"))
                {
                    eventBlock.Tapped = () => GoToBranches(repoId);
					eventBlock.Header.Add(new TextBlock(" created branch "));
                    eventBlock.Header.Add(new AnchorBlock(createEvent.Ref, eventBlock.Tapped));

                    if (ReportRepository)
                    {
                        eventBlock.Header.Add(new TextBlock(" in "));
                        eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                    }
                }
                else if (createEvent.RefType.Equals("tag"))
                {
                    eventBlock.Tapped = () => GoToTagsCommand.Execute(eventModel.Repo);
					eventBlock.Header.Add(new TextBlock(" created tag "));
                    eventBlock.Header.Add(new AnchorBlock(createEvent.Ref, eventBlock.Tapped));

                    if (ReportRepository)
                    {
                        eventBlock.Header.Add(new TextBlock(" in "));
                        eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                    }
                }
            }


            var deleteEvent = eventModel.PayloadObject as EventModel.DeleteEvent;
            if (deleteEvent != null)
            {
                if (deleteEvent.RefType.Equals("branch"))
                {
                    eventBlock.Tapped = () => GoToBranches(repoId);
					eventBlock.Header.Add(new TextBlock(" deleted branch "));
                }
                else if (deleteEvent.RefType.Equals("tag"))
                {
                    eventBlock.Tapped = () => GoToTagsCommand.Execute(eventModel.Repo);
					eventBlock.Header.Add(new TextBlock(" deleted tag "));
                }
                else
                    return null;

                eventBlock.Header.Add(new AnchorBlock(deleteEvent.Ref, eventBlock.Tapped));
                if (!ReportRepository) return eventBlock;
                eventBlock.Header.Add(new TextBlock(" in "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                return eventBlock;
            }


            if (eventModel.PayloadObject is EventModel.DownloadEvent)
            {
                // Don't show the download event for now...
                return null;
            }


            var followEvent = eventModel.PayloadObject as EventModel.FollowEvent;
            if (followEvent != null)
            {
                eventBlock.Tapped = () => GoToUser(followEvent.Target.Login);
				eventBlock.Header.Add(new TextBlock(" started following "));
                eventBlock.Header.Add(new AnchorBlock(followEvent.Target.Login, eventBlock.Tapped));
                return eventBlock;
            }
            /*
             * FORK EVENT
             */
            else if (eventModel.PayloadObject is EventModel.ForkEvent)
            {
                var forkEvent = (EventModel.ForkEvent)eventModel.PayloadObject;
                var forkedRepo = new EventModel.RepoModel {Id = forkEvent.Forkee.Id, Name = forkEvent.Forkee.FullName, Url = forkEvent.Forkee.Url};
                eventBlock.Tapped = () => GoToRepositoryCommand.Execute(forkedRepo);
				eventBlock.Header.Add(new TextBlock(" forked "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                eventBlock.Header.Add(new TextBlock(" to "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(forkedRepo));
            }
            /*
             * FORK APPLY EVENT
             */
            else if (eventModel.PayloadObject is EventModel.ForkApplyEvent)
            {
                var forkEvent = (EventModel.ForkApplyEvent)eventModel.PayloadObject;
                eventBlock.Tapped = () => GoToRepositoryCommand.Execute(eventModel.Repo);
				eventBlock.Header.Add(new TextBlock(" applied fork to "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                eventBlock.Header.Add(new TextBlock(" on branch "));
                eventBlock.Header.Add(new AnchorBlock(forkEvent.Head, () => GoToBranches(repoId)));
            }
            /*
             * GIST EVENT
             */
            else if (eventModel.PayloadObject is EventModel.GistEvent)
            {
                var gistEvent = (EventModel.GistEvent)eventModel.PayloadObject;
                eventBlock.Tapped = () => GoToGistCommand.Execute(gistEvent);

                if (string.Equals(gistEvent.Action, "create", StringComparison.OrdinalIgnoreCase))
					eventBlock.Header.Add(new TextBlock(" created Gist #"));
                else if (string.Equals(gistEvent.Action, "update", StringComparison.OrdinalIgnoreCase))
					eventBlock.Header.Add(new TextBlock(" updated Gist #"));

                eventBlock.Header.Add(new AnchorBlock(gistEvent.Gist.Id, eventBlock.Tapped));
                eventBlock.Body.Add(new TextBlock(gistEvent.Gist.Description.Replace('\n', ' ').Replace("\r", "").Trim()));
            }
            /*
             * GOLLUM EVENT (WIKI)
             */
            else if (eventModel.PayloadObject is EventModel.GollumEvent)
            {
                //var gistEvent = (EventModel.GollumEvent)eventModel.PayloadObject;
                //                var action = elementAction = () => NavigationController.PushViewController(new GistInfoViewController(gistEvent.Gist.Id), true);
                //                if (string.Equals(gistEvent.Action, "create", StringComparison.OrdinalIgnoreCase))
                //                {
                //                    eventBlock.Header.Add(new TextBlock("Created Gist #"));
                //                    eventBlock.Header.Add(new TextBlock(gistEvent.Gist.Id, () => action()));
                //                    eventBlock.Header.Add(new TextBlock(": " + gistEvent.Gist.Description));
                //                }
                //                else if (string.Equals(gistEvent.Action, "update", StringComparison.OrdinalIgnoreCase))
                //                {
                //                    eventBlock.Header.Add(new TextBlock("Updated Gist #"));
                //                    eventBlock.Header.Add(new TextBlock(gistEvent.Gist.Id, () => action()));
                //                    eventBlock.Header.Add(new TextBlock(": " + gistEvent.Gist.Description));
                //                }
            }
            /*
             * ISSUE COMMENT EVENT
             */
            else if (eventModel.PayloadObject is EventModel.IssueCommentEvent)
            {
                var commentEvent = (EventModel.IssueCommentEvent)eventModel.PayloadObject;
                eventBlock.Tapped = () => GoToIssue(repoId, commentEvent.Issue.Number);

                if (commentEvent.Issue.PullRequest != null && !string.IsNullOrEmpty(commentEvent.Issue.PullRequest.HtmlUrl))
					eventBlock.Header.Add(new TextBlock(" commented on pull request "));
                else
					eventBlock.Header.Add(new TextBlock(" commented on issue "));

                eventBlock.Header.Add(new AnchorBlock("#" + commentEvent.Issue.Number, eventBlock.Tapped));
                eventBlock.Header.Add(new TextBlock(" in "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));

                eventBlock.Body.Add(new TextBlock(commentEvent.Comment.Body.Replace('\n', ' ').Replace("\r", "").Trim()));
            }
            /*
             * ISSUES EVENT
             */
            else if (eventModel.PayloadObject is EventModel.IssuesEvent)
            {
                var issueEvent = (EventModel.IssuesEvent)eventModel.PayloadObject;
                eventBlock.Tapped  = () => GoToIssue(repoId, issueEvent.Issue.Number);

                if (string.Equals(issueEvent.Action, "opened", StringComparison.OrdinalIgnoreCase))
                    eventBlock.Header.Add(new TextBlock(" opened issue "));
                else if (string.Equals(issueEvent.Action, "closed", StringComparison.OrdinalIgnoreCase))
					eventBlock.Header.Add(new TextBlock(" closed issue "));
                else if (string.Equals(issueEvent.Action, "reopened", StringComparison.OrdinalIgnoreCase))
					eventBlock.Header.Add(new TextBlock(" reopened issue "));

                eventBlock.Header.Add(new AnchorBlock("#" + issueEvent.Issue.Number, eventBlock.Tapped));
                eventBlock.Header.Add(new TextBlock(" in "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                eventBlock.Body.Add(new TextBlock(issueEvent.Issue.Title.Trim()));
            }
            /*
             * MEMBER EVENT
             */
            else if (eventModel.PayloadObject is EventModel.MemberEvent)
            {
                var memberEvent = (EventModel.MemberEvent)eventModel.PayloadObject;
                eventBlock.Tapped = () => GoToRepositoryCommand.Execute(eventModel.Repo);

                if (memberEvent.Action.Equals("added"))
                    eventBlock.Header.Add(new TextBlock(" added as a collaborator"));
                else if (memberEvent.Action.Equals("removed"))
                    eventBlock.Header.Add(new TextBlock(" removed as a collaborator"));

                if (ReportRepository)
                {
                    eventBlock.Header.Add(new TextBlock(" to "));
                    eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                }
            }
            /*
             * PUBLIC EVENT
             */
            else if (eventModel.PayloadObject is EventModel.PublicEvent)
            {
                eventBlock.Tapped = () => GoToRepositoryCommand.Execute(eventModel.Repo);
                if (ReportRepository)
                {
                    eventBlock.Header.Add(new TextBlock(" has open sourced "));
                    eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                }
                else
                    eventBlock.Header.Add(new TextBlock(" has been open sourced this repository!"));
            }
            /*
             * PULL REQUEST EVENT
             */
            else if (eventModel.PayloadObject is EventModel.PullRequestEvent)
            {
                var pullEvent = (EventModel.PullRequestEvent)eventModel.PayloadObject;
                eventBlock.Tapped = () => GoToPullRequest(repoId, pullEvent.Number);

                if (pullEvent.Action.Equals("closed"))
                    eventBlock.Header.Add(new TextBlock(" closed pull request "));
                else if (pullEvent.Action.Equals("opened"))
                    eventBlock.Header.Add(new TextBlock(" opened pull request "));
                else if (pullEvent.Action.Equals("synchronize"))
                    eventBlock.Header.Add(new TextBlock(" synchronized pull request "));
                else if (pullEvent.Action.Equals("reopened"))
                    eventBlock.Header.Add(new TextBlock(" reopened pull request "));

                eventBlock.Header.Add(new AnchorBlock("#" + pullEvent.PullRequest.Number, eventBlock.Tapped));
                eventBlock.Header.Add(new TextBlock(" in "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));

                eventBlock.Body.Add(new TextBlock(pullEvent.PullRequest.Title));
            }
            /*
             * PULL REQUEST REVIEW COMMENT EVENT
             */
            else if (eventModel.PayloadObject is EventModel.PullRequestReviewCommentEvent)
            {
                var commentEvent = (EventModel.PullRequestReviewCommentEvent)eventModel.PayloadObject;
                eventBlock.Tapped = () => GoToPullRequests(repoId);
                eventBlock.Header.Add(new TextBlock(" commented on pull request "));
                if (ReportRepository)
                {
                    eventBlock.Header.Add(new TextBlock(" in "));
                    eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                }

                eventBlock.Body.Add(new TextBlock(commentEvent.Comment.Body.Replace('\n', ' ').Replace("\r", "").Trim()));
            }
            /*
             * PUSH EVENT
             */
            else if (eventModel.PayloadObject is EventModel.PushEvent)
            {
                var pushEvent = (EventModel.PushEvent)eventModel.PayloadObject;

                if (eventModel.Repo != null)
                    eventBlock.Tapped = () => GoToChangeset(repoId, pushEvent.Commits[0].Sha);

                eventBlock.Header.Add(new TextBlock(" pushed to "));
                if (!string.IsNullOrEmpty(pushEvent.Ref))
                {
                    var lastSlash = pushEvent.Ref.LastIndexOf("/", StringComparison.Ordinal) + 1;
                    eventBlock.Header.Add(new AnchorBlock(pushEvent.Ref.Substring(lastSlash), () => GoToBranches(repoId)));
                }

                if (ReportRepository)
                {
                    eventBlock.Header.Add(new TextBlock(" at "));
                    eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                }

				if (pushEvent.Commits != null)
				{
					foreach (var commit in pushEvent.Commits)
					{
						var desc = (commit.Message ?? "");
						var firstNewLine = desc.IndexOf("\n");
						if (firstNewLine <= 0)
							firstNewLine = desc.Length;

						desc = desc.Substring(0, firstNewLine);
						var shortSha = commit.Sha;
						if (shortSha.Length > 6)
							shortSha = shortSha.Substring(0, 6);

						eventBlock.Body.Add(new AnchorBlock(shortSha, () => GoToChangeset(repoId, commit.Sha)));
						eventBlock.Body.Add(new TextBlock(" - " + desc + "\n"));
					}
				}
            }


            var teamAddEvent = eventModel.PayloadObject as EventModel.TeamAddEvent;
            if (teamAddEvent != null)
            {
                eventBlock.Header.Add(new TextBlock(" added "));

                if (teamAddEvent.User != null)
                    eventBlock.Header.Add(new AnchorBlock(teamAddEvent.User.Login, () => GoToUser(teamAddEvent.User.Login)));
                else if (teamAddEvent.Repo != null)
                    eventBlock.Header.Add(CreateRepositoryTextBlock(new EventModel.RepoModel { Id = teamAddEvent.Repo.Id, Name = teamAddEvent.Repo.FullName, Url = teamAddEvent.Repo.Url }));
                else
                    return null;

                if (teamAddEvent.Team == null) return eventBlock;
                eventBlock.Header.Add(new TextBlock(" to team "));
                eventBlock.Header.Add(new AnchorBlock(teamAddEvent.Team.Name, () => { }));
                return eventBlock;
            }


            var watchEvent = eventModel.PayloadObject as EventModel.WatchEvent;
            if (watchEvent != null)
            {
                eventBlock.Tapped = () => GoToRepositoryCommand.Execute(eventModel);
                eventBlock.Header.Add(watchEvent.Action.Equals("started") ? 
                    new TextBlock(" started watching ") : new TextBlock(" stopped watching "));
                eventBlock.Header.Add(CreateRepositoryTextBlock(eventModel.Repo));
                return eventBlock;
            }

            return eventBlock;
        }

        private TextBlock CreateRepositoryTextBlock(EventModel.RepoModel repoModel)
        {
            //Most likely indicates a deleted repository
            if (repoModel == null)
                return new TextBlock("Unknown Repository");
            if (repoModel.Name == null)
                return new TextBlock("<Deleted Repository>");

            var repoSplit = repoModel.Name.Split('/');
            if (repoSplit.Length < 2)
                return new TextBlock(repoModel.Name);

            var repoOwner = repoSplit[0];
            var repoName = repoSplit[1];
			return !repoOwner.ToLower().Equals(this.GetApplication().Account.Username.ToLower()) ? 
                new AnchorBlock(repoModel.Name, () => GoToRepositoryCommand.Execute(repoModel)) : 
                new AnchorBlock(repoName, () => GoToRepositoryCommand.Execute(repoModel));
        }


        public class EventBlock
        {
            public IList<TextBlock> Header { get; private set; }
            public IList<TextBlock> Body { get; private set; } 
            public Action Tapped { get; set; }

            public EventBlock()
            {
                Header = new List<TextBlock>(6);
                Body = new List<TextBlock>();
            }
        }

        public class TextBlock
        {
            public string Text { get; set; }

            public TextBlock()
            {
            }

            public TextBlock(string text)
            {
                Text = text;
            }
        }

        public class AnchorBlock : TextBlock
        {
            public AnchorBlock(string text, Action tapped) : base(text)
            {
                Tapped = tapped;
            }

            public Action Tapped { get; set; }

            public AnchorBlock(Action tapped)
            {
                Tapped = tapped;
            }
        }
    }
}