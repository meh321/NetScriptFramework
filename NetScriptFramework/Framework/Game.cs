using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NetScriptFramework
{
    /// <summary>
    /// Contains header information about current game.
    /// </summary>
    public abstract class Game
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Game"/> class.
        /// </summary>
        public Game()
        {
            if (System.Threading.Interlocked.Exchange(ref Main.GameCreate, 0) != 1)
                throw new InvalidOperationException("Game header information can not be manually created! Use Main.Game property to fetch current game information.");
        }

        #endregion

        #region Game members

        /// <summary>
        /// Gets the short name of current game. For example "Skyrim".
        /// </summary>
        /// <value>
        /// The short name.
        /// </value>
        public abstract string ShortName
        {
            get;
        }

        /// <summary>
        /// Gets the full name of current game. For example "The Elder Scrolls V: Skyrim"
        /// </summary>
        /// <value>
        /// The full name.
        /// </value>
        public abstract string FullName
        {
            get;
        }

        /// <summary>
        /// Gets the name of the executable of current game including file extension. For example "TESV.exe".
        /// </summary>
        /// <value>
        /// The name of the executable.
        /// </value>
        public abstract string ExecutableName
        {
            get;
        }

        /// <summary>
        /// Gets the name of the target module. This is usually equal to ExecutableName but sometimes we may want to target a DLL inside the process instead in which case they would be different.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public abstract string ModuleName
        {
            get;
        }

        /// <summary>
        /// Gets the version library hash that is required to be loaded.
        /// </summary>
        /// <value>
        /// The version library hash.
        /// </value>
        public abstract ulong VersionLibraryHash
        {
            get;
        }

        /// <summary>
        /// Gets the version of current game. It is read from the executable. This is a list of four integers always.
        /// The first integer in the list is the most significant version number and last is the least significant.
        /// For example { 1, 9, 32, 0 }
        /// </summary>
        /// <value>
        /// The version of game.
        /// </value>
        public abstract IReadOnlyList<int> GameVersion
        {
            get;
        }

        /// <summary>
        /// Gets the library version. This is separate from game's version. Multiple library versions may exist for the same
        /// version of the game.
        /// </summary>
        /// <value>
        /// The library version.
        /// </value>
        public abstract int LibraryVersion
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether currently loaded game version is a valid version supported by this library.
        /// This is only used during game header initialization. If it returns false the game will abort and display an error.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is valid version; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsValidVersion
        {
            get;
        }

        /// <summary>
        /// Initializes the game library.
        /// </summary>
        protected virtual void Initialize()
        {

        }

        private readonly Dictionary<uint, Type> _InterfaceTypeMap = new Dictionary<uint, Type>();
        private readonly Dictionary<uint, Type> _ImplementationTypeMap = new Dictionary<uint, Type>();
        private readonly Dictionary<ulong, Type> _ImplementationVidMap = new Dictionary<ulong, Type>();
        private readonly Dictionary<ulong, Type> _InterfaceVidMap = new Dictionary<ulong, Type>();

        /// <summary>
        /// Gets the implementation by identifier.
        /// </summary>
        /// <param name="vid">The vid.</param>
        /// <returns></returns>
        internal Type GetImplementationById(ulong vid)
        {
            Type t = null;
            if (this._ImplementationVidMap.TryGetValue(vid, out t))
                return t;
            return null;
        }

        /// <summary>
        /// Gets the interface by identifier.
        /// </summary>
        /// <param name="vid">The vid.</param>
        /// <returns></returns>
        public Type GetInterfaceById(ulong vid)
        {
            Type t = null;
            if (this._InterfaceVidMap.TryGetValue(vid, out t))
                return t;
            return null;
        }

        /// <summary>
        /// Registers the interface type.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="type">The type.</param>
        /// <param name="vid">The unique identifier of type.</param>
        protected void RegisterInterfaceType(uint id, Type type, ulong vid)
        {
            if (this._InterfaceTypeMap.ContainsKey(id))
                throw new ArgumentException("An interface with the identifier " + id + " was already registered!");

            this._InterfaceTypeMap[id] = type;
            if (vid != 0 && !this._InterfaceVidMap.ContainsKey(vid))
                this._InterfaceVidMap[vid] = type;
        }

        /// <summary>
        /// Registers the implementation type.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="type">The type.</param>
        /// <param name="vid">The unique identifier of type.</param>
        protected void RegisterImplementationType(uint id, Type type, ulong vid)
        {
            if (this._ImplementationTypeMap.ContainsKey(id))
                throw new ArgumentException("An implementation with identifier " + id + " was already registered!");

            this._ImplementationTypeMap[id] = type;
            if(vid != 0)
                this._ImplementationVidMap[vid] = type;
        }

        internal void _initialize()
        {
            this.RegisterType(IntPtr.Zero, typeof(unknown), typeof(impl_unknown), null, 0);
            this.RegisterType(IntPtr.Zero, typeof(VoidGenericArgument), typeof(impl_VoidGenericArgument), null, 0);

            this.Initialize();

            var info = Main.GameInfo;
            if (info != null)
            {
                var module = Main.GetMainTargetedModule();
                var ptr = module.BaseAddress;
                foreach (var r in info.registrationList)
                {
                    Type interfaceType;
                    Type implementationType;

                    if (!this._InterfaceTypeMap.TryGetValue(r.InterfaceId, out interfaceType) || interfaceType == null)
                        throw new ArgumentOutOfRangeException("Didn't find interface type with identifier " + r.InterfaceId + " in game library!");
                    if (!this._ImplementationTypeMap.TryGetValue(r.ImplementationId, out implementationType) || implementationType == null)
                        throw new ArgumentOutOfRangeException("Didn't find implementation type with identifier " + r.ImplementationId + " in game library!");

                    IntPtr? vtable = null;
                    if (r.VTableOffset >= 0)
                        vtable = ptr + r.VTableOffset;
                    this.RegisterType(ptr, interfaceType, implementationType, vtable, r.OffsetInType);
                }
            }
        }

        /// <summary>
        /// The types cache.
        /// </summary>
        internal readonly TypeCache Types = new TypeCache();

        /// <summary>
        /// Gets the module version.
        /// </summary>
        /// <param name="ver">The version.</param>
        /// <returns></returns>
        public static int[] GetModuleVersion(System.Diagnostics.FileVersionInfo ver)
        {
            int[] arr;
            if ((arr = Game.ParseVersion(ver.ProductVersion, 0, 0, 0, 0)) != null && arr.Length != 0) return arr;
            if ((arr = Game.ParseVersion(ver.FileVersion, 0, 0, 0, 0)) != null && arr.Length != 0) return arr;
            if ((arr = Game.ParseVersion(null, ver.ProductMajorPart, ver.ProductMinorPart, ver.ProductBuildPart, ver.ProductPrivatePart)) != null && arr.Length != 0) return arr;
            if ((arr = Game.ParseVersion(null, ver.FileMajorPart, ver.FileMinorPart, ver.FileBuildPart, ver.FilePrivatePart)) != null && arr.Length != 0) return arr;
            return null;
        }

        /// <summary>
        /// Parses the version.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="a">a.</param>
        /// <param name="b">b.</param>
        /// <param name="c">c.</param>
        /// <param name="d">d.</param>
        /// <returns></returns>
        public static int[] ParseVersion(string str, int a, int b, int c, int d)
        {
            if(!string.IsNullOrEmpty(str))
            {
                string[] spl = str.Split(new[] { "." }, StringSplitOptions.None);
                if (spl.Length > 4 || spl.Length == 0)
                    return null;

                int[] result = new int[4];
                for(int i = 0; i < result.Length; i++)
                {
                    int t = 0;
                    if (!int.TryParse(spl[i], out t) || t < 0 || t >= 65536)
                        return null;
                    result[i] = t;
                }
                if (IsBadVersion(result))
                    return null;
                return result;
            }

            {
                int[] result = new int[] { a, b, c, d };
                if (IsBadVersion(result))
                    return null;
                return result;
            }
        }

        /// <summary>
        /// Determines whether the version is bad or missing possibly.
        /// </summary>
        /// <param name="r">The version.</param>
        /// <returns></returns>
        private static bool IsBadVersion(int[] r)
        {
            if (r.All(q => q == 0))
                return true;

            if(r[0] == 1)
            {
                bool yes = true;
                for(int i = 1; i < r.Length; i++)
                {
                    if(r[i] != 0)
                    {
                        yes = false;
                        break;
                    }
                }

                if (yes)
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// Registers the type to library. This must be done for all types.
        /// </summary>
        /// <param name="module">The module base address where this type is in. If zero then it's current process main module.</param>
        /// <param name="interfaceType">Type of the interface.</param>
        /// <param name="implementationType">Type of the implementation.</param>
        /// <param name="vtable">The virtual function table address.</param>
        /// <param name="offsetInFullType">The offset of this vtable in full type.</param>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// </exception>
        internal void RegisterType(IntPtr module, Type interfaceType, Type implementationType, IntPtr? vtable, int offsetInFullType)
        {
            if (module == IntPtr.Zero)
                module = Main.GetMainTargetedModule().BaseAddress;
            if (interfaceType == null)
                throw new ArgumentNullException("interfaceType");
            if (implementationType == null)
                throw new ArgumentNullException("implementationType");
            if (!interfaceType.IsInterface)
                throw new ArgumentException("Interface type must be an interface!", "interfaceType");
            if (interfaceType != typeof(IMemoryObject) && !typeof(IMemoryObject).IsAssignableFrom(interfaceType))
                throw new ArgumentException("Interface type must inherit from IMemoryObject interface!", "interfaceType");
            if (implementationType != typeof(MemoryObject) && !implementationType.IsSubclassOf(typeof(MemoryObject)))
                throw new ArgumentException("Implementation type must inherit from MemoryObject!", "implementationType");
            if (implementationType.IsAbstract)
                throw new ArgumentException("Implementation type must not be abstract!", "implementationType");
            if (!interfaceType.IsAssignableFrom(implementationType))
                throw new ArgumentException("Interface type must be assignable from implementation! The implementation is `" + implementationType.Name + "` and interface is `" + interfaceType.Name + "`.");
            if (offsetInFullType < 0)
                throw new ArgumentOutOfRangeException("offsetInFullType");

            // This is actually normal and should be allowed!
            //if (offsetInFullType > 0 && !vtable.HasValue) throw new ArgumentException("Can't set offset in full type without setting vtable address! The implementation is `" + implementationType.Name + "` and interface is `" + interfaceType.Name + "`.");

            TypeDescriptor t = null;

            ConstructorInfo ci = null;
            {
                var cis = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(q => !q.IsStatic && q.GetParameters().Length == 0).ToList();
                if (cis.Count != 1)
                    throw new ArgumentException("A valid parameterless constructor was not found on type \"" + implementationType.Name + "\"!");
                ci = cis[0];
            }

            t = new TypeDescriptor();
            t.InterfaceType = interfaceType;
            t.ImplementationType = implementationType;
            t.VTable = vtable;
            t.OffsetInFullType = offsetInFullType;
            t.Module = module;
            
            var builder = new System.Reflection.Emit.DynamicMethod("Creator", typeof(MemoryObject), new Type[0], true);
            var generator = builder.GetILGenerator();
            generator.Emit(System.Reflection.Emit.OpCodes.Newobj, ci);
            generator.Emit(System.Reflection.Emit.OpCodes.Ret);
            t.Creator = (TypeDescriptor.CreatorDelegate)builder.CreateDelegate(typeof(TypeDescriptor.CreatorDelegate));

            List<TypeDescriptor> ls = null;
            if (!this.Types.TypesByImplementation.TryGetValue(t.ImplementationType, out ls))
            {
                ls = new List<TypeDescriptor>(2);
                this.Types.TypesByImplementation[t.ImplementationType] = ls;
            }
            ls.Add(t);
            
            this.Types.All.Add(t);

            if (vtable.HasValue)
            {
                if (this.Types.TypesByVTable.ContainsKey(vtable.Value))
                    throw new ArgumentException("Multiple type registrations with same vtable address! (" + t.InterfaceType.Name + ")");
                this.Types.TypesByVTable[vtable.Value] = t;
                this.Types.TypesWithVTable.Add(t.InterfaceType);
            }
            else
            {
                if (this.Types.TypesByNoVTable.ContainsKey(t.InterfaceType))
                    throw new ArgumentException("Multiple type registrations with same interface type and without vtable address! (" + t.InterfaceType.Name + ")");
                this.Types.TypesByNoVTable[t.InterfaceType] = t;
            }
        }
        
        #endregion
    }

    #region TypeCache class

    /// <summary>
    /// The registered type cache for library.
    /// </summary>
    internal sealed class TypeCache
    {
        /// <summary>
        /// The registered types by interface.
        /// </summary>
        internal readonly Dictionary<Type, TypeDescriptor> TypesByNoVTable = new Dictionary<Type, TypeDescriptor>();

        /// <summary>
        /// The types by virtual function table address.
        /// </summary>
        internal readonly Dictionary<IntPtr, TypeDescriptor> TypesByVTable = new Dictionary<IntPtr, TypeDescriptor>();

        /// <summary>
        /// The types with virtual function table.
        /// </summary>
        internal readonly HashSet<Type> TypesWithVTable = new HashSet<Type>();

        /// <summary>
        /// The types by implementation.
        /// </summary>
        internal readonly Dictionary<Type, List<TypeDescriptor>> TypesByImplementation = new Dictionary<Type, List<TypeDescriptor>>();
        
        /// <summary>
        /// All types.
        /// </summary>
        internal readonly List<TypeDescriptor> All = new List<TypeDescriptor>();
    }

    #endregion

    #region TypeDescriptor class

    /// <summary>
    /// Implement registered type info.
    /// </summary>
    internal sealed class TypeDescriptor
    {
        /// <summary>
        /// The interface type.
        /// </summary>
        internal Type InterfaceType;

        /// <summary>
        /// The implementation (internal) type.
        /// </summary>
        internal Type ImplementationType;
        
        /// <summary>
        /// The virtual function table address.
        /// </summary>
        internal IntPtr? VTable;

        /// <summary>
        /// The offset in full type.
        /// </summary>
        internal int OffsetInFullType;

        /// <summary>
        /// The module base address where this type is in.
        /// </summary>
        internal IntPtr Module;

        /// <summary>
        /// The constructor for implementation.
        /// </summary>
        internal CreatorDelegate Creator;

        /// <summary>
        /// The delegate for constructing the object.
        /// </summary>
        /// <returns></returns>
        internal delegate MemoryObject CreatorDelegate();
    }

    #endregion

    #region Value generic argument handlers

    /// <summary>
    /// Base interface for a generic argument that is a constant value.
    /// </summary>
    public interface IValueGenericArgument
    {
        /// <summary>
        /// Gets the base value.
        /// </summary>
        /// <value>
        /// The base value.
        /// </value>
        object BaseValue
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant boolean value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IBoolValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        bool Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant character value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface ICharValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        char Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant int8 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IInt8ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        sbyte Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant uint8 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IUInt8ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        byte Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant int16 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IInt16ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        short Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant uint16 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IUInt16ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        ushort Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant int32 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IInt32ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        int Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant uint32 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IUInt32ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        uint Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant int64 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IInt64ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        long Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant uint64 value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IUInt64ValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        ulong Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant float value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IFloatValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        float Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant double value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IDoubleValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        double Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for a constant pointer value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IIntPtrValueGenericArgument : IValueGenericArgument
    {
        /// <summary>
        /// Gets the value.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        IntPtr Value
        {
            get;
        }
    }

    /// <summary>
    /// Interface for an unknown value generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IValueGenericArgument" />
    public interface IUnknownValueGenericArgument : IValueGenericArgument
    {

    }

    #endregion

    #region Caching

    /// <summary>
    /// This is a helper class for caching an address value.
    /// </summary>
    public struct CachedVid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedVid"/> struct.
        /// </summary>
        /// <param name="_arg">The argument.</param>
        private CachedVid(IntPtr? _arg)
        {
            this._result = _arg;
        }

        /// <summary>
        /// The cached result.
        /// </summary>
        private readonly IntPtr? _result;

        /// <summary>
        /// Gets the value. This will throw an exception if the value is not initialized correctly!
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        /// <exception cref="System.NotSupportedException">Trying to use an address that failed to initialize! This could mean the code being executed is not supported in current version of application.</exception>
        public IntPtr Value
        {
            get
            {
                if (!this._result.HasValue)
                    throw new NotSupportedException("Trying to use an address that failed to initialize! This could mean the code being executed is not supported in current version of application.");

                return this._result.Value;
            }
        }

        /// <summary>
        /// Tries to get the value. This will not throw an exception.
        /// </summary>
        /// <returns></returns>
        public IntPtr? TryGetValue()
        {
            return this._result;
        }

        /// <summary>
        /// Initializes the specified address. This will throw an exception if failed to find.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="extraOffset">The extra offset.</param>
        /// <param name="patternOffset">The pattern offset.</param>
        /// <param name="pattern">The pattern.</param>
        /// <exception cref="System.ArgumentException">Unable to initialize address with unique ID of  + id + !</exception>
        public static CachedVid Initialize(ulong id, int extraOffset = 0, int patternOffset = 0, string pattern = null)
        {
            var r = Main.GameInfo != null ? Main.GameInfo.TryGetAddressOf(id, extraOffset, patternOffset, pattern) : null;
            if (!r.HasValue)
                throw new ArgumentException("Unable to initialize address with unique ID of " + id + "!");

            return new CachedVid(r.Value);
        }

        /// <summary>
        /// Tries to initialize the specified address. This will return false if failed to find.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="extraOffset">The extra offset.</param>
        /// <param name="patternOffset">The pattern offset.</param>
        /// <param name="pattern">The pattern.</param>
        /// <returns></returns>
        public static CachedVid TryInitialize(ulong id, int extraOffset = 0, int patternOffset = 0, string pattern = null)
        {
            var r = Main.GameInfo != null ? Main.GameInfo.TryGetAddressOf(id, extraOffset, patternOffset, pattern) : null;
            return new CachedVid(r);
        }
    }

    /// <summary>
    /// Helper class for caching field offsets.
    /// </summary>
    public struct CachedFid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedFid"/> struct.
        /// </summary>
        /// <param name="_arg">The argument.</param>
        private CachedFid(int? _arg)
        {
            this._result = _arg;
        }

        /// <summary>
        /// The cached result.
        /// </summary>
        private readonly int? _result;

        /// <summary>
        /// Gets the value. This will throw an exception if the value is not initialized correctly!
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        /// <exception cref="System.NotSupportedException">Trying to use an address that failed to initialize! This could mean the code being executed is not supported in current version of application.</exception>
        public int Value
        {
            get
            {
                if (!this._result.HasValue)
                    throw new NotSupportedException("Trying to use a field offset that failed to initialize! This could mean the code being executed is not supported in current version of application.");

                return this._result.Value;
            }
        }

        /// <summary>
        /// Tries to get the value. This will not throw an exception.
        /// </summary>
        /// <returns></returns>
        public int? TryGetValue()
        {
            return this._result;
        }

        /// <summary>
        /// Initializes the specified field offset. This will throw an exception if failed to find.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        /// <param name="fieldId">The field identifier.</param>
        /// <exception cref="System.ArgumentException">Unable to initialize address with unique ID of  + id + !</exception>
        public static CachedFid Initialize(ulong typeId, uint fieldId)
        {
            var t = Main.GameInfo != null ? Main.GameInfo.GetTypeInfo(typeId) : null;
            if (t == null)
                throw new ArgumentException("Unable to initialize field offset due to type with unique ID of " + typeId + " was not found!");

            var ls = t.Fields;
            GameInfo.GameFieldInfo fld = null;
            if(ls != null)
            {
                // Save some time, usually field id is also index.
                if(fieldId > 0 && fieldId <= int.MaxValue)
                {
                    int index = (int)fieldId - 1;
                    if(index < ls.Count)
                    {
                        var x = ls[index];
                        if (x.FieldId == fieldId)
                            fld = x;
                    }
                }

                if(fld == null)
                {
                    foreach(var x in ls)
                    {
                        if(x.FieldId == fieldId)
                        {
                            fld = x;
                            break;
                        }
                    }
                }
            }

            if (fld == null)
                throw new ArgumentException("Unable to initialize field offset due to field with ID " + fieldId + " was not found in type " + (t.Name ?? "") + " (" + t.Id + ")!");

            if (!fld.Begin.HasValue)
                throw new ArgumentException("Unable to initialize field offset due to field with ID " + fieldId + " in type " + (t.Name ?? "") + " (" + t.Id + ") did not have a known offset!");

            return new CachedFid(fld.Begin.Value);
        }

        /// <summary>
        /// Tries to initializes the specified field offset.
        /// </summary>
        /// <param name="typeId">The type identifier.</param>
        /// <param name="fieldId">The field identifier.</param>
        /// <returns></returns>
        public static CachedFid TryInitialize(ulong typeId, uint fieldId)
        {
            var t = Main.GameInfo != null ? Main.GameInfo.GetTypeInfo(typeId) : null;
            if (t != null)
            {
                var ls = t.Fields;
                if (ls != null)
                {
                    GameInfo.GameFieldInfo fld = null;

                    // Save some time, usually field id is also index.
                    if (fieldId > 0 && fieldId <= int.MaxValue)
                    {
                        int index = (int)fieldId - 1;
                        if (index < ls.Count)
                        {
                            var x = ls[index];
                            if (x.FieldId == fieldId)
                                fld = x;
                        }
                    }

                    if (fld == null)
                    {
                        foreach (var x in ls)
                        {
                            if (x.FieldId == fieldId)
                            {
                                fld = x;
                                break;
                            }
                        }
                    }

                    if (fld != null && fld.Begin.HasValue)
                        return new CachedFid(fld.Begin.Value);
                }
            }

            return new CachedFid();
        }
    }

    /// <summary>
    /// A cached library value.
    /// </summary>
    public struct CachedLibValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedLibValue"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        public CachedLibValue(int? value)
        {
            this._value = value;
        }

        /// <summary>
        /// The value.
        /// </summary>
        private readonly int? _value;
        
        /// <summary>
        /// Gets the value. This may throw an exception if the value is not valid.
        /// </summary>
        /// <value>
        /// The value.
        /// </value>
        /// <exception cref="System.NotSupportedException">Version specific value was not found in version library! This could mean the function or field being accessed is not supported in current version of executable!</exception>
        public int Value
        {
            get
            {
                if (!this._value.HasValue)
                    throw new NotSupportedException("Version specific value was not found in version library! This could mean the function or field being accessed is not supported in current version of executable!");

                return this._value.Value;
            }
        }

        /// <summary>
        /// Gets the value safely. This will not throw any exception.
        /// </summary>
        /// <value>
        /// The value safe.
        /// </value>
        public int? ValueSafe
        {
            get
            {
                return this._value;
            }
        }
    }

    #endregion
}
