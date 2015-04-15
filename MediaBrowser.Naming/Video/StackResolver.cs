﻿using MediaBrowser.Naming.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Interfaces.IO;
using Patterns.Logging;

namespace MediaBrowser.Naming.Video
{
    public class StackResolver
    {
        private readonly NamingOptions _options;
        private readonly ILogger _logger;
        private readonly IRegexProvider _iRegexProvider;

        public StackResolver(NamingOptions options, ILogger logger)
            : this(options, logger, new RegexProvider())
        {
        }

        public StackResolver(NamingOptions options, ILogger logger, IRegexProvider iRegexProvider)
        {
            _options = options;
            _logger = logger;
            _iRegexProvider = iRegexProvider;
        }

        public StackResult ResolveDirectories(IEnumerable<string> files)
        {
            return Resolve(files.Select(i => new FileMetadata
            {
                Id = i,
                IsFolder = true
            }));
        }

        public StackResult ResolveFiles(IEnumerable<string> files)
        {
            return Resolve(files.Select(i => new FileMetadata
            {
                Id = i,
                IsFolder = false
            }));
        }

        public StackResult Resolve(IEnumerable<FileMetadata> files)
        {
            var result = new StackResult();

            var resolver = new VideoResolver(_options, _logger);

            var list = files
                .Where(i => i.IsFolder || (resolver.IsVideoFile(i.Id) || resolver.IsStubFile(i.Id)))
                .OrderBy(i => i.Id)
                .ToList();

            var expressions = _options.VideoFileStackingExpressions;

            for (var i = 0; i < list.Count; i++)
            {
                var offset = 0;

                var file1 = list[i];

                var expressionIndex = 0;
                while (expressionIndex < expressions.Count)
                {
                    var exp = expressions[expressionIndex];
                    var stack = new FileStack
                    {
                        Expression = exp
                    };

                    // (Title)(Volume)(Ignore)(Extension)
                    var match1 = FindMatch(file1, exp, offset);

                    if (match1.Success)
                    {
                        var title1 = match1.Groups[1].Value;
                        var volume1 = match1.Groups[2].Value;
                        var ignore1 = match1.Groups[3].Value;
                        var extension1 = match1.Groups[4].Value;

                        var j = i + 1;
                        while (j < list.Count)
                        {
                            var file2 = list[j];

                            if (file1.IsFolder != file2.IsFolder)
                            {
                                j++;
                                continue;
                            }

                            // (Title)(Volume)(Ignore)(Extension)
                            var match2 = FindMatch(file2, exp, offset);

                            if (match2.Success)
                            {
                                var title2 = match2.Groups[1].Value;
                                var volume2 = match2.Groups[2].Value;
                                var ignore2 = match2.Groups[3].Value;
                                var extension2 = match2.Groups[4].Value;

                                if (string.Equals(title1, title2, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!string.Equals(volume1, volume2, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (string.Equals(ignore1, ignore2, StringComparison.OrdinalIgnoreCase) &&
                                            string.Equals(extension1, extension2, StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (stack.Files.Count == 0)
                                            {
                                                stack.Name = title1 + ignore1;
                                                stack.IsFolderStack = file1.IsFolder;
                                                //stack.Name = title1 + ignore1 + extension1;
                                                stack.Files.Add(file1.Id);
                                            }
                                            stack.Files.Add(file2.Id);
                                        }
                                        else 
                                        {
                                            // Sequel
                                            offset = 0;
                                            expressionIndex++;
                                            break;
                                        }
                                    }
                                    else if (!string.Equals(ignore1, ignore2, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // False positive, try again with offset
                                        offset = match1.Groups[3].Index;
                                        break;
                                    }
                                    else
                                    {
                                        // Extension mismatch
                                        offset = 0;
                                        expressionIndex++;
                                        break;
                                    }
                                }
                                else
                                {
                                    // Title mismatch
                                    offset = 0;
                                    expressionIndex++;
                                    break;
                                }
                            }
                            else
                            {
                                // No match 2, next expression
                                offset = 0;
                                expressionIndex++;
                                break;
                            }

                            j++;
                        }

                        if (j == list.Count)
                        {
                            expressionIndex = expressions.Count;
                        }
                    }
                    else
                    {
                        // No match 1
                        offset = 0;
                        expressionIndex++;
                    }

                    if (stack.Files.Count > 1)
                    {
                        result.Stacks.Add(stack);
                        i += stack.Files.Count - 1;
                        break;
                    }
                }
            }

            return result;
        }

        private string GetRegexInput(FileMetadata file)
        {
            // For directories, dummy up an extension otherwise the expressions will fail
            var input = !file.IsFolder
                ? file.Id
                : file.Id + ".mkv";

            return Path.GetFileName(input);
        }

        private Match FindMatch(FileMetadata input, string expression, int offset)
        {
            var regexInput = GetRegexInput(input);

            if (offset < 0 || offset >= regexInput.Length)
            {
                return Match.Empty;
            }

            var regex = _iRegexProvider.GetRegex(expression, RegexOptions.IgnoreCase);
            return regex.Match(regexInput, offset);
        }
    }
}
