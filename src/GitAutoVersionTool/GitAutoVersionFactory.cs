using System;
using System.IO;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.IO;

namespace GitAutoVersionTool
{
    public static class GitAutoVersionFactory
    {
        const string _CONFIG_FILE = ".gitautoversion.json";
        static readonly PathConstruction.AbsolutePath _rootPath = NukeBuild.RootDirectory;
        static readonly PathConstruction.AbsolutePath _configPath = _rootPath / _CONFIG_FILE;
        static readonly DateTime _dateTime = DateTime.UtcNow;
        static readonly string _env = EnvironmentInfo.MachineName;
        static GitAutoVersion GitAutoVersion;

        static readonly object _padlock = new object();

        public static GitAutoVersion Create()
        {
            if (GitAutoVersion != null) return GitAutoVersion;
            lock (_padlock)
            {
                return GitAutoVersion ?? (GitAutoVersion = GitAutoVersionFactoryInternal.CreateInternal());
            }
        }
        public static GitAutoVersion Create(int buildCounter, DateTime buildDate)
        {
            if (GitAutoVersion != null) return GitAutoVersion;
            lock (_padlock)
            {
                return GitAutoVersion ?? (GitAutoVersion = GitAutoVersionFactoryInternal.CreateInternal(buildCounter, buildDate));
            }
        }
        public static GitAutoVersion CreateLegacy(int major, int minor, int buildCounter, DateTime buildDate)
        {
            if (GitAutoVersion != null) return GitAutoVersion;
            lock (_padlock)
            {
                return GitAutoVersion ?? (GitAutoVersion = GitAutoVersionFactoryInternal.
                           CreateLegacyInternal(major,minor, buildCounter, buildDate));
            }
        }

        internal static class GitAutoVersionFactoryInternal
        {
            public static GitAutoVersion CreateInternal()
            {
                var config = Read();

                var data = GitTool.GetAllGitData();
                var baseVersion = GetBaseGitAutoVersion("", 0, _dateTime, _env);
                var ver = CalculateVersion(baseVersion, data, config);
                return ver;
            }

            public static GitAutoVersion CreateInternal(int buildCounter, DateTime dateTime)
            {
                var config = Read();
                var data = GitTool.GetAllGitData();
                var baseVersion = GetBaseGitAutoVersion("", buildCounter, dateTime, _env);
                var ver = CalculateVersion(baseVersion, data, config);
                return ver;
            }

            public static GitAutoVersion CreateLegacyInternal(int major, int minor, int buildCounter, DateTime date)
            {
                var simple = new GitAutoVersionSimple(major,
                    minor,
                    buildCounter,
                    "", buildCounter, date, _env);

                var data = GitTool.GetAllGitData();
                return new GitAutoVersion(data, simple);
            }


            static GitAutoVersion CalculateVersion(GitAutoVersion baseVersion, GitAutoVersionGitSubData data, Config config)
            {
                var branch = data.GitBranch;
                if (config.Branches.ContainsKey(branch))
                {
                    var configBranch = config.Branches[branch];
                    var firstParentNumber = GitTool.GetCommitNumberCurrentBranchFirstParent(configBranch.ParentSha);
                    var sem = SemVersion.Parse(configBranch.Version);
                    var patchNewValue = sem.Patch + firstParentNumber - 1;
                    var pathNewValue2 = patchNewValue < 0 ? 0 : patchNewValue;
                    var simple = new GitAutoVersionSimple(sem.Major, sem.Minor,
                        pathNewValue2,
                        baseVersion.Special, baseVersion.BuildCounter, baseVersion.DateTime, baseVersion.Env);
                    return new GitAutoVersion(data, simple);
                }
                else
                {
                    var firstParentNumber = data.GitCommitsCurrentBranchFirstParent;
                    var simple = new GitAutoVersionSimple(baseVersion.Major, baseVersion.Minor,
                        firstParentNumber, baseVersion.Special, baseVersion.BuildCounter, baseVersion.DateTime,
                        baseVersion.Env);

                    return new GitAutoVersion(data, simple);
                }
            }

            static GitAutoVersion GetBaseGitAutoVersion(string special, int buildCounter, DateTime dateTime, string env)
            {
                var data = GitTool.GetAllGitData();
                var simple = new GitAutoVersionSimple(0, 0, 0, special, buildCounter, dateTime, env);
                var baseVersion = new GitAutoVersion(data, simple);
                return baseVersion;
            }

            static Config Read()
            {
                if (File.Exists(_configPath) == false)
                {
                    Logger.Trace($"Config is not present: {_configPath}");
                    return new Config();
                }

                try
                {
                    var json = File.ReadAllText(_configPath);
                    var o = JsonConvert.DeserializeObject<Config>(json);
                    return o;
                }
                catch (Exception)
                {
                    Logger.Error($"Config is not valid: {_configPath}");
                    return new Config();
                }
            }
        }
    }
}