using System;

namespace NowUI
{
    /// <summary>
    /// Separates otherwise identical paths owned by different NowUI subsystems.
    /// Values are explicit because they are part of the runtime identity format.
    /// </summary>
    internal enum NowIdDomain : byte
    {
        None = 0,
        OwnerRoot = 1,
        Scope = 2,
        Control = 3,
        Layout = 4,
        State = 5,
        Overlay = 6,
        FocusHost = 7,
        Effect = 8,
        Occurrence = 9,
        ExtensionPath = 10,
        Legacy = 11
    }

    internal static class NowIdHash
    {
        enum SegmentKind : byte
        {
            OwnerNonce = 1,
            AuthoredString = 2,
            AuthoredInt = 3,
            CallSite = 4,
            Occurrence = 5,
            LegacyInt = 6,
            DomainBoundary = 7
        }

        const ulong GoldenRatio = 0x9E3779B97F4A7C15UL;
        const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;
        const ulong MixMultiplier2 = 0x94D049BB133111EBUL;

        const ulong OwnerSeed = 0xD2B74407B1CE6E93UL;
        const ulong DomainSeed = 0xCA5A826395121157UL;
        const ulong SegmentSeed = 0x8CB92BA72F3D8DD7UL;
        const ulong StringSeed = 0xDB4F0B9175AE2165UL;
        const ulong IntSeed = 0xBBE0563303A4615FUL;
        const ulong CallSiteSeed = 0xA0F2EC75A1FE1575UL;
        const ulong EdgeSeed = 0x89E182857D9ED689UL;
        const ulong ZeroReplacement = 0xA0761D6478BD642FUL;

        // Domain and segment salts are pure functions of two tiny enums, yet
        // every derivation used to recompute both through Avalanche. Controls
        // derive several ids per frame, so the salts are tabulated once.
        static readonly ulong[] DomainSalts = BuildDomainSalts();

        static readonly ulong[] SegmentSalts = BuildSegmentSalts();

        static readonly NowResolvedId LegacyRoot = CreateRoot(
            0x4E6F7755494C6567UL,
            SegmentKind.LegacyInt);

        static ulong[] BuildDomainSalts()
        {
            var salts = new ulong[(int)NowIdDomain.Legacy + 1];

            for (int i = 0; i < salts.Length; ++i)
            {
                unchecked
                {
                    salts[i] = Avalanche(DomainSeed + ((ulong)i * GoldenRatio));
                }
            }

            return salts;
        }

        static ulong[] BuildSegmentSalts()
        {
            var salts = new ulong[(int)SegmentKind.DomainBoundary + 1];

            for (int i = 0; i < salts.Length; ++i)
            {
                unchecked
                {
                    salts[i] = Avalanche(SegmentSeed + ((ulong)i * MixMultiplier2));
                }
            }

            return salts;
        }

        internal static NowResolvedId CreateOwnerRoot(ulong ownerNonce)
        {
            if (ownerNonce == 0UL)
                throw new ArgumentOutOfRangeException(nameof(ownerNonce), "Owner nonce 0 is reserved.");

            return CreateRoot(ownerNonce, SegmentKind.OwnerNonce);
        }

        internal static NowResolvedId Derive(
            NowResolvedId parent,
            NowIdDomain domain,
            NowId segment)
        {
            ValidateParentAndDomain(parent, domain);

            if (!segment.hasValue)
                throw new ArgumentException("A resolved child path requires a non-empty authored id.", nameof(segment));

            if (segment.isString)
            {
                return DeriveHashed(
                    parent,
                    domain,
                    SegmentKind.AuthoredString,
                    HashString(segment.stringValue));
            }

            return DeriveHashed(
                parent,
                domain,
                SegmentKind.AuthoredInt,
                HashInt(segment.intValue));
        }

        internal static NowResolvedId Derive(
            NowResolvedId parent,
            NowIdDomain domain,
            string segment)
        {
            if (segment == null)
                throw new ArgumentNullException(nameof(segment));

            return Derive(parent, domain, new NowId(segment));
        }

        internal static NowResolvedId Derive(
            NowResolvedId parent,
            NowIdDomain domain,
            int segment)
        {
            return Derive(parent, domain, new NowId(segment));
        }

        internal static NowResolvedId EnterDomain(
            NowResolvedId parent,
            NowIdDomain domain)
        {
            ValidateParentAndDomain(parent, domain);
            return DeriveHashed(
                parent,
                domain,
                SegmentKind.DomainBoundary,
                Avalanche(DomainSeed ^ SegmentSeed));
        }

        internal static NowResolvedId DeriveCallSite(
            NowResolvedId parent,
            NowIdDomain domain,
            string file,
            int line)
        {
            ValidateParentAndDomain(parent, domain);

            if (line < 0)
                throw new ArgumentOutOfRangeException(nameof(line), "Call-site line cannot be negative.");

            return DeriveHashed(parent, domain, SegmentKind.CallSite, HashCallSite(file, line));
        }

        // Call-site tokens can be captured before an authored id overrides the
        // fallback. Hashing must therefore leave validation until resolution.
        internal static ulong HashCallSite(string file, int line)
        {
            ulong fileHash = file != null
                ? HashString(file)
                : Avalanche(StringSeed ^ GoldenRatio);
            ulong lineHash = HashInt(line);
            return Avalanche(fileHash ^ RotateLeft(lineHash, 31) ^ CallSiteSeed);
        }

        internal static NowResolvedId DeriveCallSiteHash(
            NowResolvedId parent,
            NowIdDomain domain,
            ulong payload,
            int line)
        {
            ValidateParentAndDomain(parent, domain);

            if (line < 0)
                throw new ArgumentOutOfRangeException(nameof(line), "Call-site line cannot be negative.");

            return DeriveHashed(parent, domain, SegmentKind.CallSite, payload);
        }

        internal static NowResolvedId DeriveCallSiteToken(
            NowResolvedId parent,
            NowIdDomain domain,
            int token)
        {
            ValidateParentAndDomain(parent, domain);
            return DeriveHashed(
                parent,
                domain,
                SegmentKind.CallSite,
                Avalanche(HashInt(token) ^ CallSiteSeed));
        }

        internal static NowResolvedId DeriveOccurrence(
            NowResolvedId parent,
            int occurrence)
        {
            if (occurrence <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(occurrence),
                    "Only repeated occurrences are derived; the first occurrence keeps its base id.");

            return DeriveHashed(
                parent,
                NowIdDomain.Occurrence,
                SegmentKind.Occurrence,
                HashInt(occurrence));
        }

        internal static NowResolvedId FromLegacy(int value)
        {
            if (value == 0)
                return NowResolvedId.None;

            return DeriveHashed(
                LegacyRoot,
                NowIdDomain.Legacy,
                SegmentKind.LegacyInt,
                HashInt(value));
        }

        internal static int AuthoredHashCode(NowId id)
        {
            if (!id.hasValue)
                return 0;

            ulong hash = id.isString
                ? Avalanche(HashString(id.stringValue) ^ SegmentSalt(SegmentKind.AuthoredString))
                : Avalanche(HashInt(id.intValue) ^ SegmentSalt(SegmentKind.AuthoredInt));
            return unchecked((int)hash ^ (int)(hash >> 32));
        }

        static NowResolvedId CreateRoot(ulong nonce, SegmentKind kind)
        {
            unchecked
            {
                ulong kindSalt = SegmentSalt(kind);
                ulong value = Avalanche(
                    Avalanche(nonce ^ OwnerSeed) +
                    RotateLeft(DomainSalt(NowIdDomain.OwnerRoot), 17) +
                    RotateLeft(kindSalt, 41));
                return Resolved(value);
            }
        }

        static NowResolvedId DeriveHashed(
            NowResolvedId parent,
            NowIdDomain domain,
            SegmentKind kind,
            ulong payload)
        {
            ValidateParentAndDomain(parent, domain);

            unchecked
            {
                ulong ancestry = Avalanche(parent.value ^ EdgeSeed);
                ulong taggedDomain = RotateLeft(DomainSalt(domain), 17);
                ulong taggedKind = RotateLeft(SegmentSalt(kind), 41);
                ulong value = Avalanche(
                    ancestry + taggedDomain + taggedKind + RotateLeft(payload, 29));
                return Resolved(value);
            }
        }

        static void ValidateParentAndDomain(NowResolvedId parent, NowIdDomain domain)
        {
            if (!parent.hasValue)
                throw new ArgumentException("A resolved child path requires a non-empty parent.", nameof(parent));

            byte domainValue = (byte)domain;

            if (domainValue <= (byte)NowIdDomain.OwnerRoot ||
                domainValue > (byte)NowIdDomain.Legacy)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(domain),
                    "A concrete non-root identity domain is required.");
            }
        }

        static ulong HashString(string value)
        {
            unchecked
            {
                ulong hash = StringSeed ^ ((ulong)value.Length * GoldenRatio);

                for (int i = 0; i < value.Length; ++i)
                {
                    hash ^= (ulong)value[i] + SegmentSeed;
                    hash = RotateLeft(hash, 27) * MixMultiplier1 + MixMultiplier2;
                }

                return Avalanche(hash ^ ((ulong)value.Length * MixMultiplier2));
            }
        }

        static ulong HashInt(int value)
        {
            return Avalanche(IntSeed ^ unchecked((uint)value));
        }

        static ulong DomainSalt(NowIdDomain domain)
        {
            int index = (int)domain;

            if ((uint)index < (uint)DomainSalts.Length)
                return DomainSalts[index];

            unchecked
            {
                return Avalanche(DomainSeed + ((ulong)domain * GoldenRatio));
            }
        }

        static ulong SegmentSalt(SegmentKind kind)
        {
            int index = (int)kind;

            if ((uint)index < (uint)SegmentSalts.Length)
                return SegmentSalts[index];

            unchecked
            {
                return Avalanche(SegmentSeed + ((ulong)kind * MixMultiplier2));
            }
        }

        static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }

        static ulong Avalanche(ulong value)
        {
            unchecked
            {
                value ^= value >> 30;
                value *= MixMultiplier1;
                value ^= value >> 27;
                value *= MixMultiplier2;
                value ^= value >> 31;
                return value;
            }
        }

        static NowResolvedId Resolved(ulong value)
        {
            return new NowResolvedId(value != 0UL ? value : ZeroReplacement);
        }
    }
}
