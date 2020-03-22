﻿using System;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

using static Mirror.Tests.AsyncUtil;
using UnityEngine.Events;

namespace Mirror.Tests
{
    public class NetworkIdentityTests
    {
       #region test components
        class MyTestComponent : NetworkBehaviour
        {
            internal bool onStartServerInvoked;

            public void OnStartServer()
            {
                onStartServerInvoked = true;
            }
        }

        class StartAuthorityCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public void OnStartAuthority()
            {
                ++called;
            }
        }

        class StopAuthorityCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public void OnStopAuthority()
            {
                ++called;
            }
        }

        class StartLocalPlayerExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public void OnStartLocalPlayer()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class StartLocalPlayerCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public void OnStartLocalPlayer()
            {
                ++called;
            }
        }

        class NetworkDestroyExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public void OnNetworkDestroy()
            {
                ++called;
                throw new Exception("some exception");
            }
        }

        class NetworkDestroyCalledNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public void OnNetworkDestroy()
            {
                ++called;
            }
        }

        class SetHostVisibilityExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public bool valuePassed;
            public override void OnSetHostVisibility(bool visible)
            {
                ++called;
                valuePassed = visible;
                throw new Exception("some exception");
            }
        }

        class CheckObserverExceptionNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public NetworkConnection valuePassed;
            public override bool OnCheckObserver(NetworkConnection conn)
            {
                ++called;
                valuePassed = conn;
                throw new Exception("some exception");
            }
        }

        class CheckObserverTrueNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override bool OnCheckObserver(NetworkConnection conn)
            {
                ++called;
                return true;
            }
        }

        class CheckObserverFalseNetworkBehaviour : NetworkBehaviour
        {
            public int called;
            public override bool OnCheckObserver(NetworkConnection conn)
            {
                ++called;
                return false;
            }
        }

        class SerializeTest1NetworkBehaviour : NetworkBehaviour
        {
            public int value;
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                writer.WriteInt32(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadInt32();
            }
        }

        class SerializeTest2NetworkBehaviour : NetworkBehaviour
        {
            public string value;
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                writer.WriteString(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadString();
            }
        }

        class SerializeExceptionNetworkBehaviour : NetworkBehaviour
        {
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                throw new Exception("some exception");
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                throw new Exception("some exception");
            }
        }

        class SerializeMismatchNetworkBehaviour : NetworkBehaviour
        {
            public int value;
            public override bool OnSerialize(NetworkWriter writer, bool initialState)
            {
                writer.WriteInt32(value);
                // one too many
                writer.WriteInt32(value);
                return true;
            }
            public override void OnDeserialize(NetworkReader reader, bool initialState)
            {
                value = reader.ReadInt32();
            }
        }

        class RebuildObserversNetworkBehaviour : NetworkBehaviour
        {
            public NetworkConnection observer;
            public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
            {
                observers.Add(observer);
                return true;
            }
        }

        class RebuildEmptyObserversNetworkBehaviour : NetworkBehaviour
        {
            public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
            {
                // return true so that caller knows we implemented
                // OnRebuildObservers, but return no observers
                return true;
            }

            public int hostVisibilityCalled;
            public bool hostVisibilityValue;
            public override void OnSetHostVisibility(bool visible)
            {
                ++hostVisibilityCalled;
                hostVisibilityValue = visible;
            }
        }

        #endregion

        GameObject gameObject;
        NetworkIdentity identity;
        private NetworkServer server;
        private NetworkClient client;
        private GameObject networkServerGameObject;

        [SetUp]
        public void SetUp()
        {
            networkServerGameObject = new GameObject();
            server = networkServerGameObject.AddComponent<NetworkServer>();
            client = networkServerGameObject.AddComponent<NetworkClient>();
            Transport.activeTransport = Substitute.For<Transport>();

            Transport.activeTransport.GetMaxPacketSize().ReturnsForAnyArgs(1000);

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
            identity.server = server;
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(gameObject);
            Object.DestroyImmediate(networkServerGameObject);
            Transport.activeTransport = null;
        }

        // A Test behaves as an ordinary method
        [Test]
        public void OnStartServerTest()
        {
            // lets add a component to check OnStartserver
            MyTestComponent component1 = gameObject.AddComponent<MyTestComponent>();
            MyTestComponent component2 = gameObject.AddComponent<MyTestComponent>();

            identity.OnStartServer.AddListener(component1.OnStartServer);
            identity.OnStartServer.AddListener(component2.OnStartServer);

            identity.StartServer();

            Assert.That(component1.onStartServerInvoked);
            Assert.That(component2.onStartServerInvoked);
        }

        [Test]
        public void GetSetAssetId()
        {
            // assign a guid
            var guid = new Guid(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
            identity.assetId = guid;

            // did it work?
            Assert.That(identity.assetId, Is.EqualTo(guid));
        }

        [Test]
        public void SetClientOwner()
        {
            // SetClientOwner
            (_, ULocalConnectionToClient original) = ULocalConnectionToClient.CreateLocalConnections();
            identity.SetClientOwner(original);
            Assert.That(identity.connectionToClient, Is.EqualTo(original));
        }

        [Test]
        public void SetOverrideClientOwner()
        {
            // SetClientOwner
            (_, ULocalConnectionToClient original) = ULocalConnectionToClient.CreateLocalConnections();
            identity.SetClientOwner(original);

            // setting it when it's already set shouldn't overwrite the original
            (_, ULocalConnectionToClient overwrite) = ULocalConnectionToClient.CreateLocalConnections();
            // will log a warning
            Assert.Throws<InvalidOperationException>(() =>
            {
                identity.SetClientOwner(overwrite);
            });

            Assert.That(identity.connectionToClient, Is.EqualTo(original));
        }

        [Test]
        public void RemoveObserverInternal()
        {
            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add an observer connection
            var connection = new NetworkConnectionToClient(42);
            identity.observers.Add(connection);

            // RemoveObserverInternal with invalid connection should do nothing
            identity.RemoveObserverInternal(new NetworkConnectionToClient(43));
            Assert.That(identity.observers.Count, Is.EqualTo(1));

            // RemoveObserverInternal with existing connection should remove it
            identity.RemoveObserverInternal(connection);
            Assert.That(identity.observers.Count, Is.EqualTo(0));
        }

        [Test]
        public void AssignSceneID()
        {
            // Awake will have assigned a random sceneId of format 0x00000000FFFFFFFF
            // -> make sure that one was assigned, and that the left part was
            //    left empty for scene hash
            Assert.That(identity.sceneId, !Is.Zero);
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, Is.EqualTo(0x0000000000000000));

            // make sure that Awake added it to sceneIds dict
            Assert.That(NetworkIdentity.GetSceneIdentity(identity.sceneId), !Is.Null);
        }

        [Test]
        public void SetSceneIdSceneHashPartInternal()
        {
            // Awake will have assigned a random sceneId of format 0x00000000FFFFFFFF
            // -> make sure that one was assigned, and that the left part was
            //    left empty for scene hash
            Assert.That(identity.sceneId, !Is.Zero);
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, Is.EqualTo(0x0000000000000000));
            ulong rightPart = identity.sceneId;

            // set scene hash
            identity.SetSceneIdSceneHashPartInternal();

            // make sure that the right part is still the random sceneid
            Assert.That(identity.sceneId & 0x00000000FFFFFFFF, Is.EqualTo(rightPart));

            // make sure that the left part is a scene hash now
            Assert.That(identity.sceneId & 0xFFFFFFFF00000000, !Is.Zero);
            ulong finished = identity.sceneId;

            // calling it again should said the exact same hash again
            identity.SetSceneIdSceneHashPartInternal();
            Assert.That(identity.sceneId, Is.EqualTo(finished));
        }

        [Test]
        public void OnValidateSetupIDsSetsEmptyAssetIDForSceneObject()
        {
            // OnValidate will have been called. make sure that assetId was set
            // to 0 empty and not anything else, because this is a scene object
            Assert.That(identity.assetId, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void OnStartServerCallsComponentsAndCatchesExceptions()
        {
            // make a mock delegate
            UnityAction func = Substitute.For<UnityAction>();

            // add it to the listener
            identity.OnStartServer.AddListener(func);

            // Since we are testing that exceptions are not swallowed,
            // when the mock is invoked, throw an exception 
            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // Make sure that the exception is not swallowed
            Assert.Throws<Exception>( () => { 
                identity.StartServer();
            });

            // ask the mock if it got invoked
            // if the mock is not invoked,  then this fails
            // This is a type of assert
            func.Received().Invoke();
        }

        [Test]
        public void OnStartClientCallsComponentsAndCatchesExceptions()
        {
            // add component
            UnityAction func = Substitute.For<UnityAction>();
            identity.OnStartClient.AddListener(func);

            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // make sure exceptions are not swallowed
            Assert.Throws<Exception>( () => { 
                identity.StartClient();
            });
            func.Received().Invoke();
            
            // we have checks to make sure that it's only called once.
            Assert.DoesNotThrow(() => { 
                identity.StartClient();
            });
            func.Received(1).Invoke();
        }

        [Test]
        public void OnStartAuthorityCallsComponentsAndCatchesExceptions()
        {
            // add component
            UnityAction func = Substitute.For<UnityAction>();
            identity.OnStartAuthority.AddListener(func);

            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // make sure exceptions are not swallowed
            Assert.Throws<Exception>( () => { 
                identity.StartAuthority();
            });
            func.Received(1).Invoke();
        }

        [Test]
        public void OnStopAuthorityCallsComponentsAndCatchesExceptions()
        {
            // add component
            UnityAction func = Substitute.For<UnityAction>();
            identity.OnStopAuthority.AddListener(func);

            func
                .When(f => f.Invoke())
                .Do(f => { throw new Exception("Some exception"); });

            // make sure exceptions are not swallowed
            Assert.Throws<Exception>( () => { 
                identity.StopAuthority();
            });
            func.Received(1).Invoke();
        }

        [Test]
        public void NotifyAuthorityCallsOnStartStopAuthority()
        {
            // add components
            StartAuthorityCalledNetworkBehaviour compStart = gameObject.AddComponent<StartAuthorityCalledNetworkBehaviour>();
            identity.OnStartAuthority.AddListener(compStart.OnStartAuthority);
            StopAuthorityCalledNetworkBehaviour compStop = gameObject.AddComponent<StopAuthorityCalledNetworkBehaviour>();
            identity.OnStopAuthority.AddListener(compStop.OnStopAuthority);

            // set authority from false to true, which should call OnStartAuthority
            identity.hasAuthority = true;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.True);
            // start should be called
            Assert.That(compStart.called, Is.EqualTo(1));
            // stop shouldn't
            Assert.That(compStop.called, Is.EqualTo(0));

            // set it to true again, should do nothing because already true
            identity.hasAuthority = true;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.True);
            // same as before
            Assert.That(compStart.called, Is.EqualTo(1));
            // same as before
            Assert.That(compStop.called, Is.EqualTo(0));

            // set it to false, should call OnStopAuthority
            identity.hasAuthority = false;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.False);
            // same as before
            Assert.That(compStart.called, Is.EqualTo(1));
            // stop should be called
            Assert.That(compStop.called, Is.EqualTo(1));

            // set it to false again, should do nothing because already false
            identity.hasAuthority = false;
            identity.NotifyAuthority();
            // shouldn't be touched
            Assert.That(identity.hasAuthority, Is.False);
            // same as before
            Assert.That(compStart.called, Is.EqualTo(1));
            // same as before
            Assert.That(compStop.called, Is.EqualTo(1));
        }

        [Test]
        public void OnSetHostVisibilityCallsComponentsAndCatchesExceptions()
        {
            // add component
            SetHostVisibilityExceptionNetworkBehaviour comp = gameObject.AddComponent<SetHostVisibilityExceptionNetworkBehaviour>();

            // make sure that comp.OnSetHostVisibility was called and make sure that
            // the exception was caught and not thrown in here.
            // an exception in OnSetHostVisibility should be caught, so that one
            // component's exception doesn't stop all other components from
            // being initialized
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;

            // should catch the exception internally and not throw it
            identity.OnSetHostVisibility(true);
            Assert.That(comp.called, Is.EqualTo(1));
            Assert.That(comp.valuePassed, Is.True);

            // should catch the exception internally and not throw it
            identity.OnSetHostVisibility(false);
            Assert.That(comp.called, Is.EqualTo(2));
            Assert.That(comp.valuePassed, Is.False);

            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void OnCheckObserver()
        {
            // add component
            CheckObserverExceptionNetworkBehaviour compExc = gameObject.AddComponent<CheckObserverExceptionNetworkBehaviour>();

            NetworkConnection connection = new NetworkConnectionToClient(42);

            // an exception in OnCheckObserver should be caught, so that one
            // component's exception doesn't stop all other components from
            // being checked
            // (an error log is expected though)
            LogAssert.ignoreFailingMessages = true;
            // should catch the exception internally and not throw it
            bool result = identity.OnCheckObserver(connection);
            Assert.That(result, Is.True);
            Assert.That(compExc.called, Is.EqualTo(1));
            LogAssert.ignoreFailingMessages = false;

            // let's also make sure that the correct connection was passed, just
            // to be sure
            Assert.That(compExc.valuePassed, Is.EqualTo(connection));

            // create a networkidentity with a component that returns true
            // result should still be true.
            var gameObjectTrue = new GameObject();
            NetworkIdentity identityTrue = gameObjectTrue.AddComponent<NetworkIdentity>();
            CheckObserverTrueNetworkBehaviour compTrue = gameObjectTrue.AddComponent<CheckObserverTrueNetworkBehaviour>();
            result = identityTrue.OnCheckObserver(connection);
            Assert.That(result, Is.True);
            Assert.That(compTrue.called, Is.EqualTo(1));

            // create a networkidentity with a component that returns true and
            // one component that returns false.
            // result should still be false if any one returns false.
            var gameObjectFalse = new GameObject();
            NetworkIdentity identityFalse = gameObjectFalse.AddComponent<NetworkIdentity>();
            compTrue = gameObjectFalse.AddComponent<CheckObserverTrueNetworkBehaviour>();
            CheckObserverFalseNetworkBehaviour compFalse = gameObjectFalse.AddComponent<CheckObserverFalseNetworkBehaviour>();
            result = identityFalse.OnCheckObserver(connection);
            Assert.That(result, Is.False);
            Assert.That(compTrue.called, Is.EqualTo(1));
            Assert.That(compFalse.called, Is.EqualTo(1));

            // clean up
            GameObject.DestroyImmediate(gameObjectFalse);
            GameObject.DestroyImmediate(gameObjectTrue);
        }

        [Test]
        public void OnSerializeAndDeserializeAllSafely()
        {
            // create a networkidentity with our test components
            SerializeTest1NetworkBehaviour comp1 = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            SerializeExceptionNetworkBehaviour compExc = gameObject.AddComponent<SerializeExceptionNetworkBehaviour>();
            SerializeTest2NetworkBehaviour comp2 = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();

            // set some unique values to serialize
            comp1.value = 12345;
            comp1.syncMode = SyncMode.Observers;
            compExc.syncMode = SyncMode.Observers;
            comp2.value = "67890";
            comp2.syncMode = SyncMode.Owner;

            // serialize all
            var ownerWriter = new NetworkWriter();
            var observersWriter = new NetworkWriter();

            // serialize should propagate exceptions
            Assert.Throws<Exception>(() =>
            {
                (int ownerWritten, int observersWritten) = identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);
                // owner should have written all components
                Assert.That(ownerWritten, Is.EqualTo(3));
                // observers should have written only the observers components
                Assert.That(observersWritten, Is.EqualTo(2));
            });

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for owner - should work even if compExc throws an exception
            var reader = new NetworkReader(ownerWriter.ToArray());

            Assert.Throws<Exception>(() =>
            {
                identity.OnDeserializeAllSafely(reader, true);
            });

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all for observers - should propagate exceptions
            reader = new NetworkReader(observersWriter.ToArray());
            Assert.Throws<Exception>(() =>
            {
                identity.OnDeserializeAllSafely(reader, true);
            });
        }

        // OnSerializeAllSafely supports at max 64 components, because our
        // dirty mask is ulong and can only handle so many bits.
        [Test]
        public void OnSerializeAllSafelyShouldDetectTooManyComponents()
        {
            // add 65 components
            for (int i = 0; i < 65; ++i)
            {
                gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            }

            // try to serialize
            var ownerWriter = new NetworkWriter();
            var observersWriter = new NetworkWriter();
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);
            });

            // shouldn't have written anything because too many components
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
            Assert.That(observersWriter.Position, Is.EqualTo(0));
        }

        // OnDeserializeSafely should be able to detect and handle serialization
        // mismatches (= if compA writes 10 bytes but only reads 8 or 12, it
        // shouldn't break compB's serialization. otherwise we end up with
        // insane runtime errors like monsters that look like npcs. that's what
        // happened back in the day with UNET).
        [Test]
        public void OnDeserializeSafelyShouldDetectAndHandleDeSerializationMismatch()
        {
            // add components
            SerializeTest1NetworkBehaviour comp1 = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            SerializeMismatchNetworkBehaviour compMiss = gameObject.AddComponent<SerializeMismatchNetworkBehaviour>();
            SerializeTest2NetworkBehaviour comp2 = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();

            // set some unique values to serialize
            comp1.value = 12345;
            comp2.value = "67890";

            // serialize
            var ownerWriter = new NetworkWriter();
            var observersWriter = new NetworkWriter();
            (int ownerWritten, int observersWritten) = identity.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // reset component values
            comp1.value = 0;
            comp2.value = null;

            // deserialize all
            var reader = new NetworkReader(ownerWriter.ToArray());
            Assert.Throws<InvalidMessageException>(() =>
            {
                identity.OnDeserializeAllSafely(reader, true);
            });
        }

        [Test]
        public void OnStartLocalPlayer()
        {
            // add components
            StartLocalPlayerExceptionNetworkBehaviour compEx = gameObject.AddComponent<StartLocalPlayerExceptionNetworkBehaviour>();
            identity.OnStartLocalPlayer.AddListener(compEx.OnStartLocalPlayer);
            StartLocalPlayerCalledNetworkBehaviour comp = gameObject.AddComponent<StartLocalPlayerCalledNetworkBehaviour>();
            identity.OnStartLocalPlayer.AddListener(comp.OnStartLocalPlayer);

            // make sure our test values are set to 0
            Assert.That(compEx.called, Is.EqualTo(0));
            Assert.That(comp.called, Is.EqualTo(0));

            // make sure that comp.OnStartServer was called
            // the exception was caught and not thrown in here.
            Assert.Throws<Exception>( () => {
                identity.StartLocalPlayer();
            });
            
            Assert.That(compEx.called, Is.EqualTo(1));
            //Due to the order the listeners are added the one without exception is never called
            Assert.That(comp.called, Is.EqualTo(0));

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            Assert.DoesNotThrow( () => {
                identity.StartLocalPlayer();
            });
            // same as before?
            Assert.That(compEx.called, Is.EqualTo(1));
            //Due to the order the listeners are added the one without exception is never called
            Assert.That(comp.called, Is.EqualTo(0));
        }

        [Test]
        public void OnNetworkDestroy()
        {
            // add components
            NetworkDestroyExceptionNetworkBehaviour compEx = gameObject.AddComponent<NetworkDestroyExceptionNetworkBehaviour>();
            identity.OnNetworkDestroy.AddListener(compEx.OnNetworkDestroy);
            NetworkDestroyCalledNetworkBehaviour comp = gameObject.AddComponent<NetworkDestroyCalledNetworkBehaviour>();
            identity.OnNetworkDestroy.AddListener(comp.OnNetworkDestroy);

            // make sure our test values are set to 0
            Assert.That(compEx.called, Is.EqualTo(0));
            Assert.That(comp.called, Is.EqualTo(0));

            // we have checks to make sure that it's only called once.
            // let's see if they work.
            Assert.Throws<Exception>( () => {
                identity.NetworkDestroy();
            });

            Assert.That(compEx.called, Is.EqualTo(1));
            //Due to the order the listeners are added the one without exception is never called
            Assert.That(comp.called, Is.EqualTo(0));
        }

        [Test]
        public void AddObserver()
        {

            identity.server = server;
            // create some connections
            var connection1 = new NetworkConnectionToClient(42);
            var connection2 = new NetworkConnectionToClient(43);

            // AddObserver should return early if called before .observers was
            // created
            Assert.That(identity.observers, Is.Null);
            // error log is expected
            LogAssert.ignoreFailingMessages = true;
            identity.AddObserver(connection1);
            LogAssert.ignoreFailingMessages = false;
            Assert.That(identity.observers, Is.Null);

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // call AddObservers
            identity.AddObserver(connection1);
            identity.AddObserver(connection2);
            Assert.That(identity.observers, Is.EquivalentTo(new[] { connection1, connection2 }));

            // adding a duplicate connectionId shouldn't overwrite the original
            identity.AddObserver(connection1);
            Assert.That(identity.observers, Is.EquivalentTo(new[] { connection1, connection2 }));
        }

        [Test]
        public void ClearObservers()
        {
            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add some observers
            identity.observers.Add(new NetworkConnectionToClient(42));
            identity.observers.Add(new NetworkConnectionToClient(43));

            // call ClearObservers
            identity.ClearObservers();
            Assert.That(identity.observers.Count, Is.EqualTo(0));
        }


        [Test]
        public void Reset()
        {
            // modify it a bit
            // creates .observers and generates a netId
            identity.StartServer(); 
            uint netId = identity.netId;
            identity.connectionToClient = new NetworkConnectionToClient(1);
            identity.connectionToServer = new NetworkConnectionToServer();
            identity.observers.Add( new NetworkConnectionToClient(2));

            // calling reset shouldn't do anything unless it was marked for reset
            identity.Reset();
            Assert.That(identity.netId, Is.EqualTo(netId));
            Assert.That(identity.connectionToClient, !Is.Null);
            Assert.That(identity.connectionToServer, !Is.Null);

            // mark for reset and reset
            identity.MarkForReset();
            identity.Reset();
            Assert.That(identity.netId, Is.EqualTo(0));
            Assert.That(identity.connectionToClient, Is.Null);
            Assert.That(identity.connectionToServer, Is.Null);
        }


        [Test]
        public void ServerUpdate()
        {
            // add components
            SerializeTest1NetworkBehaviour compA = gameObject.AddComponent<SerializeTest1NetworkBehaviour>();
            // test value
            compA.value = 1337;
            // set syncInterval so IsDirty passes the interval check
            compA.syncInterval = 0;
            // one needs to sync to owner
            compA.syncMode = SyncMode.Owner;
            SerializeTest2NetworkBehaviour compB = gameObject.AddComponent<SerializeTest2NetworkBehaviour>();
            // test value
            compB.value = "test";
            // set syncInterval so IsDirty passes the interval check
            compB.syncInterval = 0;
            // one needs to sync to owner
            compB.syncMode = SyncMode.Observers;

            // call OnStartServer once so observers are created
            identity.StartServer();

            // set it dirty
            compA.SetDirtyBit(ulong.MaxValue);
            compB.SetDirtyBit(ulong.MaxValue);
            Assert.That(compA.IsDirty(), Is.True);
            Assert.That(compB.IsDirty(), Is.True);

            // calling update without observers should clear all dirty bits.
            // it would be spawned on new observers anyway.
            identity.ServerUpdate();
            Assert.That(compA.IsDirty(), Is.False);
            Assert.That(compB.IsDirty(), Is.False);

            (_, ULocalConnectionToClient owner)
                = ULocalConnectionToClient.CreateLocalConnections();
            owner.isReady = true;
            owner.isAuthenticated = true;
            owner.connectionToServer.isAuthenticated = true;
            int ownerCalled = 0;
            owner.connectionToServer.RegisterHandler<UpdateVarsMessage>(msg => ++ownerCalled);
            identity.connectionToClient = owner;

            // add an observer connection that will receive the updates
            (_, ULocalConnectionToClient observer)
                = ULocalConnectionToClient.CreateLocalConnections();
            observer.isReady = true;
            observer.isAuthenticated = true;
            observer.connectionToServer.isAuthenticated = true;
            int observerCalled = 0;
            observer.connectionToServer.RegisterHandler<UpdateVarsMessage>(msg => ++observerCalled);
            identity.observers.Add(observer);

            // set components dirty again
            compA.SetDirtyBit(ulong.MaxValue);
            compB.SetDirtyBit(ulong.MaxValue);

            // calling update should serialize all components and send them to
            // owner/observers
            identity.ServerUpdate();

            // update connections once so that messages are processed
            owner.connectionToServer.Update();
            observer.connectionToServer.Update();

            // was it received on the clients?
            Assert.That(ownerCalled, Is.EqualTo(1));
            Assert.That(observerCalled, Is.EqualTo(1));
        }

        [Test]
        public void GetNewObservers()
        {
            // add components
            RebuildObserversNetworkBehaviour compA = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compA.observer = new NetworkConnectionToClient(12);
            RebuildObserversNetworkBehaviour compB = gameObject.AddComponent<RebuildObserversNetworkBehaviour>();
            compB.observer = new NetworkConnectionToClient(13);

            // get new observers
            var observers = new HashSet<NetworkConnection>();
            bool result = identity.GetNewObservers(observers, true);
            Assert.That(result, Is.True);
            Assert.That(observers.Count, Is.EqualTo(2));
            Assert.That(observers.Contains(compA.observer), Is.True);
            Assert.That(observers.Contains(compB.observer), Is.True);
        }

        [Test]
        public void GetNewObserversClearsHashSet()
        {
            // get new observers. no observer components so it should just clear
            // it and not do anything else
            var observers = new HashSet<NetworkConnection>();
            observers.Add(new NetworkConnectionToClient(42));
            identity.GetNewObservers(observers, true);
            Assert.That(observers.Count, Is.EqualTo(0));
        }

        [Test]
        public void GetNewObserversFalseIfNoComponents()
        {
            // get new observers. no observer components so it should be false
            var observers = new HashSet<NetworkConnection>();
            bool result = identity.GetNewObservers(observers, true);
            Assert.That(result, Is.False);
        }

        [Test]
        public void AddAllReadyServerConnectionsToObservers()
        {
            var connection1 = new NetworkConnectionToClient(12) { isReady = true };
            var connection2 = new NetworkConnectionToClient(13) { isReady = false };
            // add some server connections
            server.connections[12] = connection1;
            server.connections[13] = connection2;

            // add a host connection
            (_, ULocalConnectionToClient localConnection)
                = ULocalConnectionToClient.CreateLocalConnections();
            localConnection.isReady = true;
            server.SetLocalConnection(client, localConnection);

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add all to observers. should have the two ready connections then.
            identity.AddAllReadyServerConnectionsToObservers();
            Assert.That(identity.observers, Is.EquivalentTo(new [] { connection1, localConnection }));

            // clean up
            server.RemoveLocalConnection();
            server.Shutdown();
        }

        // RebuildObservers should always add the own ready connection
        // (if any). fixes https://github.com/vis2k/Mirror/issues/692
        [Test]
        public void RebuildObserversAddsOwnReadyPlayer()
        {
            // add at least one observers component, otherwise it will just add
            // all server connections
            gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();

            // add own player connection
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            connection.isReady = true;
            identity.connectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // rebuild should at least add own ready player
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Does.Contain(identity.connectionToClient));
        }

        // RebuildObservers should always add the own ready connection
        // (if any). fixes https://github.com/vis2k/Mirror/issues/692
        [Test]
        public void RebuildObserversOnlyAddsOwnPlayerIfReady()
        {
            // add at least one observers component, otherwise it will just add
            // all server connections
            gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();

            // add own player connection that isn't ready
            (_, ULocalConnectionToClient connection) = ULocalConnectionToClient.CreateLocalConnections();
            identity.connectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // rebuild shouldn't add own player because conn wasn't set ready
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Does.Not.Contains(identity.connectionToClient));
        }

     }
}
