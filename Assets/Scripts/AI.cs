using System;
using Riptide;
using Riptide.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface AI {
    public interface AIAction {
        public void Execute(ref MainScript.Map.Entity visualization);
    }

    [Serializable]
    public class Null : AI {
        [Serializable]
        public class NullAIAction : AIAction {
            public void Execute(ref MainScript.Map.Entity visualization) {}
        }

        public AIAction getAICurrentAction() {
            return new NullAIAction();
        }
    }

    /*public class Player : AI {
        public class PlayerAction : AIAction {
            public enum PlayerActionType {
                MOVEMENT
            }

            public PlayerActionType type;
            public Vector2 position;

            public PlayerAction(Vector2 newPosition) {
                position = newPosition;
            }

            public void Execute(ref MainScript.Map.Entity visualization) {
                switch (type) {
                    case PlayerActionType.MOVEMENT:
                        visualization.Position = position;
                        Camera.main.GetComponent<NetworkManager>().Server.SendToAll(Message.Create(MessageSendMode.Unreliable, NetworkManager.MessageId.EntityMovement));
                        break;
                }
            }
        }

        public MainScript.Map.Entity entity;
        public Queue<PlayerAction> queuedActions;

        public AIAction getAICurrentAction() {
            if (queuedActions.Count == 0) {
                return new Null.NullAIAction();
            }
            return queuedActions.Dequeue();
        }
    }*/

    [Serializable]
    public class Stickman : AI {
        [Serializable]
        public class StickmanAIAction : AIAction {
            public void Execute(ref MainScript.Map.Entity visualization) {}
        }

        [Serializable]
        public class StickmanAITask {
            public enum StickmanAITaskType {
            }
        }

        public MainScript.Map.Entity entity;
        public StickmanAITask currentTask;

        /*public Stickman(ref MainScript.Map.Entity newEntity) {
            entity = newEntity;
        }*/

        public AIAction getAICurrentAction() {
            return new StickmanAIAction();
        }
    }

    public AIAction getAICurrentAction();
}