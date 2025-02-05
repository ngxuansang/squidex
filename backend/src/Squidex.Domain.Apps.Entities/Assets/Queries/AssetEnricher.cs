﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Text;
using Squidex.Domain.Apps.Core;
using Squidex.Domain.Apps.Core.Tags;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Caching;
using Squidex.Infrastructure.Json;
using Squidex.Infrastructure.Reflection;

namespace Squidex.Domain.Apps.Entities.Assets.Queries
{
    public sealed class AssetEnricher : IAssetEnricher
    {
        private readonly ITagService tagService;
        private readonly IEnumerable<IAssetMetadataSource> assetMetadataSources;
        private readonly IRequestCache requestCache;
        private readonly IUrlGenerator urlGenerator;
        private readonly IJsonSerializer jsonSerializer;

        public AssetEnricher(ITagService tagService, IEnumerable<IAssetMetadataSource> assetMetadataSources, IRequestCache requestCache,
            IUrlGenerator urlGenerator, IJsonSerializer jsonSerializer)
        {
            this.tagService = tagService;
            this.assetMetadataSources = assetMetadataSources;
            this.requestCache = requestCache;
            this.urlGenerator = urlGenerator;
            this.jsonSerializer = jsonSerializer;
        }

        public async Task<IEnrichedAssetEntity> EnrichAsync(IAssetEntity asset, Context context,
            CancellationToken ct)
        {
            Guard.NotNull(asset);
            Guard.NotNull(context);

            var enriched = await EnrichAsync(Enumerable.Repeat(asset, 1), context, ct);

            return enriched[0];
        }

        public async Task<IReadOnlyList<IEnrichedAssetEntity>> EnrichAsync(IEnumerable<IAssetEntity> assets, Context context,
            CancellationToken ct)
        {
            Guard.NotNull(assets);
            Guard.NotNull(context);

            using (Telemetry.Activities.StartActivity("ContentQueryService/EnrichAsync"))
            {
                var results = assets.Select(x => SimpleMapper.Map(x, new AssetEntity())).ToList();

                foreach (var asset in results)
                {
                    requestCache.AddDependency(asset.UniqueId, asset.Version);
                }

                if (!context.ShouldSkipAssetEnrichment())
                {
                    await EnrichTagsAsync(results, ct);

                    EnrichWithMetadataText(results);

                    if (!context.IsFrontendClient)
                    {
                        ComputeTokens(results);
                    }
                }

                return results;
            }
        }

        private void ComputeTokens(IEnumerable<IEnrichedAssetEntity> assets)
        {
            var url = urlGenerator.Root();

            foreach (var asset in assets)
            {
                var token = new
                {
                    a = asset.AppId.Name,
                    i = asset.Id.ToString(),
                    u = url
                };

                var json = jsonSerializer.Serialize(token);

                asset.EditToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            }
        }

        private void EnrichWithMetadataText(List<AssetEntity> results)
        {
            var sb = new StringBuilder();

            void Append(string? text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(text);
                }
            }

            foreach (var asset in results)
            {
                sb.Clear();

                foreach (var source in assetMetadataSources)
                {
                    foreach (var metadata in source.Format(asset))
                    {
                        Append(metadata);
                    }
                }

                Append(asset.FileSize.ToReadableSize());

                asset.MetadataText = sb.ToString();
            }
        }

        private async Task EnrichTagsAsync(List<AssetEntity> assets,
            CancellationToken ct)
        {
            foreach (var group in assets.GroupBy(x => x.AppId.Id))
            {
                ct.ThrowIfCancellationRequested();

                var tagsById = await CalculateTags(group);

                foreach (var asset in group)
                {
                    asset.TagNames = new HashSet<string>();

                    if (asset.Tags != null)
                    {
                        foreach (var id in asset.Tags)
                        {
                            if (tagsById.TryGetValue(id, out var name))
                            {
                                asset.TagNames.Add(name);
                            }
                        }
                    }
                }
            }
        }

        private async Task<Dictionary<string, string>> CalculateTags(IGrouping<DomainId, IAssetEntity> group)
        {
            var uniqueIds = group.Where(x => x.Tags != null).SelectMany(x => x.Tags).ToHashSet();

            return await tagService.DenormalizeTagsAsync(group.Key, TagGroups.Assets, uniqueIds);
        }
    }
}
