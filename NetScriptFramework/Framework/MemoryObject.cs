using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetScriptFramework
{
    #region MemoryObject class

    /// <summary>
    /// Base implementation of a wrapper for an object that exists in memory.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IMemoryObject" />
    /// <seealso cref="NetScriptFramework.Tools.IArgument" />
    public abstract class MemoryObject : IMemoryObject, Tools.IArgument
    {
        #region Constructors

        #endregion

        #region MemoryObject members

        /// <summary>
        /// Gets the base address of the object in memory.
        /// </summary>
        /// <value>
        /// The base address of object in memory.
        /// </value>
        public IntPtr Address
        {
            get;
            internal set;
        } = IntPtr.Zero;

        /// <summary>
        /// Returns true if memory object is valid and can be accessed for reading. It is possible for this to return true even if
        /// the object is not actually valid in case of bad pointers to valid memory regions, invalid cast or partially freed memory!
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        public virtual bool IsValid
        {
            get
            {
                if (this.Address == IntPtr.Zero)
                    return false;

                int size = IntPtr.Size;

                var ti = this.TypeInfo.Info;
                if (ti != null && ti.Size.HasValue)
                    size = ti.Size.Value;

                return Memory.IsValidRegion(this.Address, size, true, false, false);
            }
        }

        /// <summary>
        /// Gets the type instance information of this complete type.
        /// </summary>
        /// <value>
        /// The type instance information of the complete type.
        /// </value>
        public GameInfo.GameTypeInstanceInfo TypeInfo
        {
            get
            {
                return this.TypeInfos[0];
            }
        }

        /// <summary>
        /// Gets the type identifier of this complete type. This will be zero if not available.
        /// </summary>
        /// <value>
        /// The type identifier of the complete type.
        /// </value>
        public ulong TypeId
        {
            get
            {
                var info = this.TypeInfo;
                if (info != null && info.Info != null)
                    return info.Info.Id;
                return 0;
            }
        }

        /// <summary>
        /// Gets all the type infos of this complete type.
        /// </summary>
        /// <value>
        /// The type infos.
        /// </value>
        public abstract IReadOnlyList<GameInfo.GameTypeInstanceInfo> TypeInfos
        {
            get;
        }

        /// <summary>
        /// Get an object in memory from specified base address.
        /// </summary>
        /// <typeparam name="T">Type of object to get.</typeparam>
        /// <param name="address">The base address of object.</param>
        /// <returns></returns>
        public static T FromAddress<T>(IntPtr address) where T : IMemoryObject
        {
            var game = Main.Game;
            if(game == null)
                throw new ArgumentException("Game library is not loaded! Unable to use types.");

            if (address != IntPtr.Zero)
            {
                var type = typeof(T);
                TypeDescriptor t = null;

                // VTable types are handled differently.
                if (game.Types.TypesWithVTable.Contains(type))
                {
                    // Not using "TryRead" on purpose because bad pointer should cause exception instead of returning null!
                    var ptr = Memory.ReadPointer(address);
                    if (Main.Game.Types.TypesByVTable.TryGetValue(ptr, out t))
                    {
                        var mo = t.Creator();
                        mo.Address = address - t.OffsetInFullType;
                        object result = mo;

                        // May cause invalid cast exception and that is fine.
                        return (T)result;
                    }

                    return default(T);
                }
                else
                {
                    // Invalid type. This usually means generic argument is wrong.
                    if (!Main.Game.Types.TypesByNoVTable.TryGetValue(type, out t))
                        throw new ArgumentException("Type \"" + typeof(T).Name + "\" is not registered with game library!");

                    var mo = t.Creator();
                    mo.Address = address - t.OffsetInFullType;
                    object result = mo;
                    return (T)result;
                }
            }

            return default(T);
        }
        
        /// <summary>
        /// Get an object in memory from specified base address.
        /// </summary>
        /// <param name="t">The type of object.</param>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">t</exception>
        /// <exception cref="System.ArgumentException">
        /// Type can not be abstract!
        /// or
        /// Type must inherit from MemoryObject!
        /// </exception>
        public static IMemoryObject FromAddress(Type t, IntPtr address)
        {
            if (t == null)
                throw new ArgumentNullException("t");
            if (!t.IsInterface)
                throw new ArgumentException("Type must be interface!");
            if (t == typeof(IMemoryObject) || t == typeof(IVirtualObject) || !typeof(IMemoryObject).IsAssignableFrom(t))
                throw new ArgumentException("Type must inherit from IMemoryObject!");

            var game = Main.Game;
            if (game == null)
                throw new ArgumentException("Game library is not loaded! Unable to use types.");

            if (address != IntPtr.Zero)
            {
                TypeDescriptor td = null;

                // VTable types are handled differently.
                if (game.Types.TypesWithVTable.Contains(t))
                {
                    // Not using "TryRead" on purpose because bad pointer should cause exception instead of returning null!
                    var ptr = Memory.ReadPointer(address);
                    if (Main.Game.Types.TypesByVTable.TryGetValue(ptr, out td))
                    {
                        var mo = td.Creator();
                        mo.Address = address - td.OffsetInFullType;

                        if (!t.IsAssignableFrom(td.ImplementationType))
                            throw new InvalidCastException();
                        return mo;
                    }

                    return null;
                }
                else
                {
                    // Invalid type. This usually means generic argument is wrong.
                    if (!Main.Game.Types.TypesByNoVTable.TryGetValue(t, out td))
                        throw new ArgumentException("Type \"" + t.Name + "\" is not registered with game library!");

                    var mo = td.Creator();
                    mo.Address = address - td.OffsetInFullType;
                    return mo;
                }
            }

            return null;
        }

        /// <summary>
        /// Get an object in memory from specified base address.
        /// </summary>
        /// <param name="vid">The unique ID of type.</param>
        /// <param name="address">The address.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">Type can not be abstract!
        /// or
        /// Type must inherit from MemoryObject!</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Type with id  + vid +  was not found!</exception>
        /// <exception cref="System.InvalidCastException"></exception>
        public static IMemoryObject FromAddress(ulong vid, IntPtr address)
        {
            var game = Main.Game;
            if (game == null)
                throw new ArgumentException("Game library is not loaded! Unable to use types.");

            if (address != IntPtr.Zero)
            {
                var impl = game.GetImplementationById(vid);
                if (impl == null)
                    throw new ArgumentOutOfRangeException("Type with id " + vid + " was not found!");

                List<TypeDescriptor> ls = null;
                if (!game.Types.TypesByImplementation.TryGetValue(impl, out ls) || ls == null || ls.Count == 0)
                    throw new ArgumentException("Type " + impl.Name + " does not have any registered descriptors!");

                TypeDescriptor td = null;
                foreach(var x in ls)
                {
                    if(x.OffsetInFullType == 0)
                    {
                        td = x;
                        break;
                    }

                    if (td == null || x.OffsetInFullType < td.OffsetInFullType)
                        td = x;
                }

                // Special case, we still want to detect if the cast is valid.
                if(td.VTable.HasValue)
                    return VirtualObject.FromAddress(address);

                var mo = td.Creator();
                mo.Address = address - td.OffsetInFullType;
                return mo;
            }

            return null;
        }

        /// <summary>
        /// Returns the address if this instance was cast into another type. Returns zero if not possible to cast.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <returns></returns>
        public abstract IntPtr Cast<T>() where T : IMemoryObject;

        /// <summary>
        /// Returns the virtual function table address of specified instance. It will return zero if not possible to get this virtual function table or the virtual function table is itself zero.
        /// </summary>
        /// <typeparam name="T">Type to get table for.</typeparam>
        /// <returns></returns>
        public virtual IntPtr VTable<T>() where T : IVirtualObject
        {
            var ptr = this.Cast<T>();
            if (ptr != IntPtr.Zero)
                return Memory.ReadPointer(ptr);

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gathers the objects for crash log.
        /// </summary>
        /// <param name="gatherer">The gatherer.</param>
        public virtual void GatherObjectsForCrashLog(InterestingCrashLogObjects gatherer)
        {

        }

        /// <summary>
        /// Gathers the string for crash log.
        /// </summary>
        /// <returns></returns>
        public virtual string GatherStringForCrashLog()
        {
            return this.ToString();
        }

        #endregion
        
        #region Object members

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance. If the specified object
        /// is not a <see cref="MemoryObject" /> or is null then this method will return false!
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            var mo = obj as MemoryObject;
            if (mo != null)
                return this.Address == mo.Address;
            return false;
        }

        /// <summary>
        /// Determines whether the specified <see cref="NetScriptFramework.IMemoryObject" />, is equal to this instance. If the specified object
        /// is not a <see cref="NetScriptFramework.IMemoryObject" /> or is null then this method will return false!
        /// </summary>
        /// <param name="obj">The <see cref="NetScriptFramework.IMemoryObject" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="NetScriptFramework.IMemoryObject" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public bool Equals(IMemoryObject obj)
        {
            var mo = obj as MemoryObject;
            if (mo != null)
                return this.Address == mo.Address;
            return false;
        }
        
        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var ti = this.TypeInfo.Info;
            return ti != null ? (ti.Name ?? "unknown") : "unknown";
        }

        #endregion

        #region IArgument members

        /// <summary>
        /// Parse an argument from this object.
        /// </summary>
        /// <param name="key">Keyword for argument.</param>
        /// <param name="message">Message to parse for.</param>
        /// <param name="parser">Parser that is currently processing message.</param>
        /// <returns></returns>
        public Tools.IArgument ParseArgument(string key, Tools.Message message, Tools.Parser parser)
        {
            var prop = this._GetProperty(key);
            if (prop == null || prop.GetMethod == null)
                return null;

            object instance = prop.GetMethod.IsStatic ? null : this;
            return prop.GetMethod.Invoke(instance, new object[0]) as Tools.IArgument;
        }

        /// <summary>
        /// Parse a variable from this object.
        /// </summary>
        /// <param name="key">Keyword for variable.</param>
        /// <param name="message">Message to parse for.</param>
        /// <param name="parser">Parser that is currently processing message.</param>
        /// <returns></returns>
        public string ParseVariable(string key, Tools.Message message, Tools.Parser parser)
        {
            var prop = this._GetProperty(key);
            if (prop == null || prop.GetMethod == null)
                return null;

            object instance = prop.GetMethod.IsStatic ? null : this;
            object result = prop.GetMethod.Invoke(instance, new object[0]);
            if (object.ReferenceEquals(result, null))
                return null;
            if (result is float)
                return ((float)result).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (result is double)
                return ((double)result).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (result is IntPtr)
                return ((IntPtr)result).ToHexString();
            return result.ToString();
        }

        /// <summary>
        /// Parse a function from this object.
        /// </summary>
        /// <param name="key">Keyword for function.</param>
        /// <param name="args">Arguments for function.</param>
        /// <param name="message">Message to parse for.</param>
        /// <param name="parser">Parser that is currently processing message.</param>
        /// <returns></returns>
        public string ParseFunction(string key, string[] args, Tools.Message message, Tools.Parser parser)
        {
            return null;
        }

        /// <summary>
        /// Get a property of this object by its name.
        /// </summary>
        /// <param name="name">The name of property.</param>
        /// <returns></returns>
        private System.Reflection.PropertyInfo _GetProperty(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var t = this.GetType();
            while(t != null)
            {
                try
                {
                    var prop = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly);
                    if (prop != null)
                        return prop;
                }
                catch(System.Reflection.AmbiguousMatchException)
                {

                }

                t = t.BaseType;
            }

            return null;
        }

        #endregion
    }

    #endregion

    #region IMemoryObject interface

    /// <summary>
    /// Base implementation of a wrapper for an object that exists in memory. Use the Equals methods to check
    /// equality with another object rather than equality operator.
    /// </summary>
    public interface IMemoryObject : IEquatable<IMemoryObject>
    {
        /// <summary>
        /// Gets the base address of the complete object in memory.
        /// </summary>
        /// <value>
        /// The base address of object in memory.
        /// </value>
        IntPtr Address
        {
            get;
        }

        /// <summary>
        /// Returns true if memory object is valid and can be accessed. It is possible for this to return true even if
        /// the object is not actually valid in case of bad pointers or freed memory!
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        bool IsValid
        {
            get;
        }

        /// <summary>
        /// Gets the type instance information of this complete type.
        /// </summary>
        /// <value>
        /// The type instance information of the complete type.
        /// </value>
        GameInfo.GameTypeInstanceInfo TypeInfo
        {
            get;
        }

        /// <summary>
        /// Gets the type identifier of this complete type. This will be zero if not available.
        /// </summary>
        /// <value>
        /// The type identifier of the complete type.
        /// </value>
        ulong TypeId
        {
            get;
        }

        /// <summary>
        /// Gets all the type infos of this complete type.
        /// </summary>
        /// <value>
        /// The type infos.
        /// </value>
        IReadOnlyList<GameInfo.GameTypeInstanceInfo> TypeInfos
        {
            get;
        }

        /// <summary>
        /// Returns the address if this instance was cast into another type. Returns zero if not possible to cast.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <returns></returns>
        IntPtr Cast<T>() where T : IMemoryObject;

        /// <summary>
        /// Returns the virtual function table address of specified instance. It will return zero if not possible to get this virtual function table or the virtual function table is itself zero.
        /// </summary>
        /// <typeparam name="T">Type to get table for.</typeparam>
        /// <returns></returns>
        IntPtr VTable<T>() where T : IVirtualObject;

        /// <summary>
        /// Gathers the objects for crash log.
        /// </summary>
        /// <param name="gatherer">The gatherer.</param>
        void GatherObjectsForCrashLog(InterestingCrashLogObjects gatherer);

        /// <summary>
        /// Gathers the string for crash log.
        /// </summary>
        /// <returns></returns>
        string GatherStringForCrashLog();
    }

    #endregion

    #region Unknown type

    /// <summary>
    /// This is an unknown type.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IMemoryObject" />
    public interface unknown : IMemoryObject
    {

    }

    /// <summary>
    /// The implementation for unknown type.
    /// </summary>
    /// <seealso cref="NetScriptFramework.MemoryObject" />
    internal sealed class impl_unknown : MemoryObject, unknown
    {
        /// <summary>
        /// Gets all the type infos of this complete type.
        /// </summary>
        /// <value>
        /// The type infos.
        /// </value>
        public override IReadOnlyList<GameInfo.GameTypeInstanceInfo> TypeInfos
        {
            get
            {
                return _TypeInfos;
            }
        }

        /// <summary>
        /// The type info.
        /// </summary>
        private static readonly GameInfo.GameTypeInstanceInfo[] _TypeInfos = new GameInfo.GameTypeInstanceInfo[] { new GameInfo.GameTypeInstanceInfo(0, null, null) };

        /// <summary>
        /// Returns the address if this instance was cast into another type. Returns zero if not possible to cast.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <returns></returns>
        public override IntPtr Cast<T>()
        {
            var t = typeof(T);
            if (t == typeof(unknown))
                return this.Address;

            return IntPtr.Zero;
        }
    }

    #endregion

    #region Void generic argument type

    /// <summary>
    /// This is a void generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.IMemoryObject" />
    public interface VoidGenericArgument : IMemoryObject
    {

    }

    /// <summary>
    /// The implementation for void generic argument.
    /// </summary>
    /// <seealso cref="NetScriptFramework.MemoryObject" />
    internal sealed class impl_VoidGenericArgument : MemoryObject, VoidGenericArgument
    {
        /// <summary>
        /// Gets all the type infos of this complete type.
        /// </summary>
        /// <value>
        /// The type infos.
        /// </value>
        public override IReadOnlyList<GameInfo.GameTypeInstanceInfo> TypeInfos
        {
            get
            {
                return _TypeInfos;
            }
        }

        /// <summary>
        /// The type info.
        /// </summary>
        private static readonly GameInfo.GameTypeInstanceInfo[] _TypeInfos = new GameInfo.GameTypeInstanceInfo[] { new GameInfo.GameTypeInstanceInfo(0, null, null) };

        /// <summary>
        /// Returns the address if this instance was cast into another type. Returns zero if not possible to cast.
        /// </summary>
        /// <typeparam name="T">Type to cast to.</typeparam>
        /// <returns></returns>
        public override IntPtr Cast<T>()
        {
            var t = typeof(T);
            if (t == typeof(VoidGenericArgument))
                return this.Address;

            return IntPtr.Zero;
        }
    }

    #endregion
}
