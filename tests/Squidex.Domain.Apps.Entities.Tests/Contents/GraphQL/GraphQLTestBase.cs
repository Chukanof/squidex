﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Security.Claims;
using FakeItEasy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NodaTime.Extensions;
using Squidex.Domain.Apps.Core;
using Squidex.Domain.Apps.Core.Apps;
using Squidex.Domain.Apps.Core.Contents;
using Squidex.Domain.Apps.Core.Schemas;
using Squidex.Domain.Apps.Entities.Apps;
using Squidex.Domain.Apps.Entities.Assets;
using Squidex.Domain.Apps.Entities.Contents.TestData;
using Squidex.Domain.Apps.Entities.Schemas;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Json;
using Squidex.Infrastructure.Json.Objects;
using Xunit;

#pragma warning disable SA1311 // Static readonly fields must begin with upper-case letter
#pragma warning disable SA1401 // Fields must be private

namespace Squidex.Domain.Apps.Entities.Contents.GraphQL
{
    public class GraphQLTestBase
    {
        protected static readonly Guid schemaId = Guid.NewGuid();
        protected static readonly Guid appId = Guid.NewGuid();
        protected static readonly string appName = "my-app";
        protected readonly Schema schemaDef;
        protected readonly IContentQueryService contentQuery = A.Fake<IContentQueryService>();
        protected readonly IAssetQueryService assetQuery = A.Fake<IAssetQueryService>();
        protected readonly ISchemaEntity schema = A.Fake<ISchemaEntity>();
        protected readonly IJsonSerializer serializer = TestUtils.CreateSerializer(TypeNameHandling.None);
        protected readonly IMemoryCache cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        protected readonly IAppProvider appProvider = A.Fake<IAppProvider>();
        protected readonly IAppEntity app = A.Dummy<IAppEntity>();
        protected readonly QueryContext context;
        protected readonly ClaimsPrincipal user = new ClaimsPrincipal();
        protected readonly IGraphQLService sut;

        public GraphQLTestBase()
        {
            schemaDef =
                new Schema("my-schema")
                    .AddJson(1, "my-json", Partitioning.Invariant,
                        new JsonFieldProperties())
                    .AddString(2, "my-string", Partitioning.Language,
                        new StringFieldProperties())
                    .AddNumber(3, "my-number", Partitioning.Invariant,
                        new NumberFieldProperties())
                    .AddAssets(4, "my-assets", Partitioning.Invariant,
                        new AssetsFieldProperties())
                    .AddBoolean(5, "my-boolean", Partitioning.Invariant,
                        new BooleanFieldProperties())
                    .AddDateTime(6, "my-datetime", Partitioning.Invariant,
                        new DateTimeFieldProperties())
                    .AddReferences(7, "my-references", Partitioning.Invariant,
                        new ReferencesFieldProperties { SchemaId = schemaId })
                    .AddReferences(9, "my-invalid", Partitioning.Invariant,
                        new ReferencesFieldProperties { SchemaId = Guid.NewGuid() })
                    .AddGeolocation(10, "my-geolocation", Partitioning.Invariant,
                        new GeolocationFieldProperties())
                    .AddTags(11, "my-tags", Partitioning.Invariant,
                        new TagsFieldProperties())
                    .AddString(12, "my-localized", Partitioning.Language,
                        new StringFieldProperties())
                    .AddArray(13, "my-array", Partitioning.Invariant, f => f
                        .AddBoolean(121, "nested-boolean")
                        .AddNumber(122, "nested-number"));

            A.CallTo(() => app.Id).Returns(appId);
            A.CallTo(() => app.Name).Returns(appName);
            A.CallTo(() => app.LanguagesConfig).Returns(LanguagesConfig.Build(Language.DE, Language.GermanGermany));

            context = QueryContext.Create(app, user);

            A.CallTo(() => schema.Id).Returns(schemaId);
            A.CallTo(() => schema.Name).Returns(schemaDef.Name);
            A.CallTo(() => schema.SchemaDef).Returns(schemaDef);
            A.CallTo(() => schema.IsPublished).Returns(true);
            A.CallTo(() => schema.ScriptQuery).Returns("<script-query>");

            var allSchemas = new List<ISchemaEntity> { schema };

            A.CallTo(() => appProvider.GetSchemasAsync(appId)).Returns(allSchemas);

            sut = new CachingGraphQLService(cache, appProvider, assetQuery, contentQuery, new FakeUrlGenerator());
        }

        protected static IContentEntity CreateContent(Guid id, Guid refId, Guid assetId, NamedContentData data = null)
        {
            var now = DateTime.UtcNow.ToInstant();

            data = data ??
                new NamedContentData()
                    .AddField("my-string",
                        new ContentFieldData()
                            .AddValue("de", "value"))
                    .AddField("my-assets",
                        new ContentFieldData()
                            .AddValue("iv", JsonValue.Array(assetId.ToString())))
                    .AddField("my-number",
                        new ContentFieldData()
                            .AddValue("iv", 1.0))
                    .AddField("my-boolean",
                        new ContentFieldData()
                            .AddValue("iv", true))
                    .AddField("my-datetime",
                        new ContentFieldData()
                            .AddValue("iv", now))
                    .AddField("my-tags",
                        new ContentFieldData()
                            .AddValue("iv", JsonValue.Array("tag1", "tag2")))
                    .AddField("my-references",
                        new ContentFieldData()
                            .AddValue("iv", JsonValue.Array(refId.ToString())))
                    .AddField("my-geolocation",
                        new ContentFieldData()
                            .AddValue("iv", JsonValue.Object().Add("latitude", 10).Add("longitude", 20)))
                    .AddField("my-json",
                        new ContentFieldData()
                            .AddValue("iv", JsonValue.Object().Add("value", 1)))
                    .AddField("my-localized",
                        new ContentFieldData()
                            .AddValue("de-DE", "de-DE"))
                    .AddField("my-array",
                        new ContentFieldData()
                            .AddValue("iv", JsonValue.Array(
                                JsonValue.Object()
                                    .Add("nested-boolean", true)
                                    .Add("nested-number", 1),
                                JsonValue.Object()
                                    .Add("nested-boolean", false)
                                    .Add("nested-number", 2))));

            var content = new ContentEntity
            {
                Id = id,
                Version = 1,
                Created = now,
                CreatedBy = new RefToken(RefTokenType.Subject, "user1"),
                LastModified = now,
                LastModifiedBy = new RefToken(RefTokenType.Subject, "user2"),
                Data = data
            };

            return content;
        }

        protected static IAssetEntity CreateAsset(Guid id)
        {
            var now = DateTime.UtcNow.ToInstant();

            var asset = new FakeAssetEntity
            {
                Id = id,
                Version = 1,
                Created = now,
                CreatedBy = new RefToken(RefTokenType.Subject, "user1"),
                LastModified = now,
                LastModifiedBy = new RefToken(RefTokenType.Subject, "user2"),
                FileName = "MyFile.png",
                FileSize = 1024,
                FileVersion = 123,
                MimeType = "image/png",
                IsImage = true,
                PixelWidth = 800,
                PixelHeight = 600
            };

            return asset;
        }

        protected void AssertResult(object expected, (bool HasErrors, object Response) result, bool checkErrors = true)
        {
            if (checkErrors && result.HasErrors)
            {
                throw new InvalidOperationException(Serialize(result));
            }

            var resultJson = serializer.Serialize(result.Response, true);
            var expectJson = serializer.Serialize(expected, true);

            Assert.Equal(expectJson, resultJson);
        }

        private string Serialize((bool HasErrors, object Response) result)
        {
            return serializer.Serialize(result);
        }
    }
}
