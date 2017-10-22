namespace Emulsion.Actors

open System
open System.Threading.Tasks

open Akka.Actor

[<AbstractClass>]
type SyncTaskWatcher() =
    inherit ReceiveActor()

    let onTaskFinished (actorType : Type) (self : IActorRef) (task : Task) : unit =
        if task.Status = TaskStatus.Faulted
        then
            Console.Error.WriteLine("{0} error: {1}", actorType, task.Exception)
            self.Tell(Akka.Actor.PoisonPill.Instance)

    abstract member RunInTask : unit -> unit

    override this.PreStart() =
        let self = this.Self
        Task.Factory
            .StartNew(this.RunInTask, TaskCreationOptions.LongRunning)
            .ContinueWith(Action<Task>(onTaskFinished (this.GetType()) self))
        |> ignore
