﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Infrastructure.Json.Objects;

namespace Squidex.Infrastructure.EventSourcing
{
    public sealed class EnvelopeHeaders : Dictionary<string, IJsonValue>
    {
        public EnvelopeHeaders()
        {
        }

        public EnvelopeHeaders(IDictionary<string, IJsonValue> headers)
            : base(headers)
        {
        }

        public EnvelopeHeaders CloneHeaders()
        {
            return new EnvelopeHeaders(this);
        }
    }
}
