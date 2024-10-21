using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Frosty.Hash;

namespace FrostySdk.Managers.Entries
{
    public enum AssetDataLocation
    {
        Cas,
        SuperBundle,
        Cache,
        CasNonIndexed
    }

    public class AssetExtraData
    {
        public Sha1 BaseSha1;
        public Sha1 DeltaSha1;
        public long DataOffset;
        public int SuperBundleId;
        public bool IsPatch;
        public string CasPath = "";
    }

    public class AssetEntry
    {
        public virtual string Name { get; set; }
        public virtual string Type { get; set; }
        public virtual string AssetType { get; }

        public virtual string DisplayName => Filename + ((IsDirty) ? "*" : "");

        public virtual string Filename {
            get {
                int id = Name.LastIndexOf('/');
                return id == -1 ? Name : Name.Substring(id + 1);
            }
        }

        public virtual string Path {
            get {
                int id = Name.LastIndexOf('/');
                return id == -1 ? "" : Name.Substring(0, id);
            }
        }

        public Sha1 Sha1;
        public Sha1 BaseSha1;

        public long Size;
        public long OriginalSize;
        public bool IsInline;
        public AssetDataLocation Location;
        public AssetExtraData ExtraData;

        public ConcurrentBag<int> Bundles = new ConcurrentBag<int>();
        public ConcurrentBag<int> AddedBundles = new ConcurrentBag<int>();
        public ConcurrentBag<int> RemBundles = new ConcurrentBag<int>();

        public ModifiedAssetEntry ModifiedEntry;
        public ConcurrentBag<AssetEntry> LinkedAssets = new ConcurrentBag<AssetEntry>();

        private bool m_dirty;

        /// <summary>
        /// returns true if this asset was added
        /// </summary>
        public bool IsAdded { get; set; }

        /// <summary>
        /// returns true if this asset or any asset linked to it is modified
        /// </summary>
        public virtual bool IsModified => IsDirectlyModified || IsIndirectlyModified;

        /// <summary>
        /// returns true if this asset (and only this asset) is modified
        /// </summary>
        public bool IsDirectlyModified => ModifiedEntry != null || AddedBundles.Count != 0 || RemBundles.Count != 0;

        /// <summary>
        /// 
        /// </summary>
        public bool HasModifiedData =>
            ModifiedEntry != null && (ModifiedEntry.Data != null || ModifiedEntry.DataObject != null);

        /// <summary>
        /// returns true if this asset is considered modified through another linked asset
        /// ie. An ebx would be considered modified if its linked resource has been modified
        /// </summary>
        public bool IsIndirectlyModified {
            get {
                foreach (AssetEntry entry in LinkedAssets)
                {
                    if (entry.IsModified)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// returns true if this asset, or any asset linked to it is dirty
        /// </summary>
        public virtual bool IsDirty {
            get {
                return m_dirty || LinkedAssets.Any(entry => entry.IsDirty);
            }
            set {
                if (m_dirty == value) return;
                m_dirty = value;
                if (m_dirty)
                {
                    OnModified();
                }
            }
        }

        /// <summary>
        /// Links the current asset to another
        /// </summary>
        public void LinkAsset(AssetEntry assetToLink)
        {
            if (!LinkedAssets.Contains(assetToLink))
            {
                LinkedAssets.Add(assetToLink);
            }

            if (assetToLink is ChunkAssetEntry entry)
            {
                if (entry.HasModifiedData)
                {
                    // store the res/ebx name in the chunk
                    entry.ModifiedEntry.H32 = Fnv1.HashString(Name.ToLower());
                }
                else
                {
                    // asset was added to bundle (so no ModifiedEntry)
                    entry.H32 = Fnv1.HashString(Name.ToLower());
                }
            }
        }

        /// <summary>
        /// Adds the current asset to the specified bundle
        /// </summary>
        public bool AddToBundle(int bid)
        {
            if (IsInBundle(bid))
            {
                return false;
            }

            AddedBundles.Add(bid);
            IsDirty = true;

            return true;
        }

        /// <summary>
        /// Adds the current asset to the specified bundles
        /// </summary>
        public bool AddToBundles(IEnumerable<int> bundles)
        {
            bool added = false;
            foreach (int bid in bundles)
            {
                if (Bundles.Contains(bid) || AddedBundles.Contains(bid)) continue;
                AddedBundles.Add(bid);
                IsDirty = true;
                added = true;
            }

            return added;
        }

        /// <summary>
        /// Returns true if asset is in the specified bundle
        /// </summary>
        public bool IsInBundle(int bid) => Bundles.Contains(bid) || AddedBundles.Contains(bid);

        /// <summary>
        /// Iterates through all bundles that the asset is a part of
        /// </summary>
        public IEnumerable<int> EnumerateBundles(bool addedOnly = false)
        {
            if (!addedOnly)
            {
                foreach (var bundle in Bundles)
                {
                    if (!RemBundles.Contains(bundle))
                    {
                        yield return bundle;
                    }
                }
            }

            foreach (var bundle in AddedBundles)
            {
                yield return bundle;
            }
        }

        public virtual void ClearModifications() => ModifiedEntry = null;

        public event EventHandler AssetModified;
        public void OnModified() => AssetModified?.Invoke(this, new EventArgs());
    }
}