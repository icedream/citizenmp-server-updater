using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace CitizenMP.Server.Installer
{
    static class RepositoryExtensions
    {
        public static void UpdateSubmodules(this IRepository git)
        {
            foreach (var submodule in git.Submodules)
            {
                var subrepoPath = Path.Combine(git.Info.WorkingDirectory, submodule.Path);
                if (!Repository.IsValid(subrepoPath))
                {
                    Directory.Delete(subrepoPath, true);
                    Repository.Clone(submodule.Url, subrepoPath);
                }

                using (var subrepo = new Repository(subrepoPath))
                {
                    subrepo.UpdateRepository(submodule.HeadCommitId.Sha);
                }
            }
        }

        public static void UpdateRepository(this IRepository git, string committishOrBranchSpec)
        {
            git.RemoveUntrackedFiles();
            git.Reset(ResetMode.Hard);
            git.Fetch(git.Network.Remotes.First().Name, new FetchOptions
            {
                TagFetchMode = TagFetchMode.None
            });
            // TODO: Check out correct branch if needed
            git.Checkout(committishOrBranchSpec, new CheckoutOptions(), Program.GitSignature);
        }
    }
}
