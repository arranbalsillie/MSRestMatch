﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class GameObjectState
{
    public string Name { get; set; }
    public string Type { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float ForwardX { get; set; }
    public float ForwardY { get; set; }
    public float Heading { get; set; }
    public float TurretHeading { get; set; }
    public float TurretForwardX { get; set; }
    public float TurretForwardY { get; set; }

    public int Health { get; set; }
    public int Ammo { get; set; }

    public override string ToString()
    {
        return Name + ":" + Type + ":" + Health + ":" + Ammo + ":" + X + ":" + Y + ":" + Heading + ":" + TurretHeading;
    }
}

public class GameSimulation
{
    GameSimRules rules;
    List<Player> activePlayers;
    TankFactory tankFactory;
    public Queue<GameCommand> enqueuedCommands;
    public List<TankController> tankControllers;
    private List<GameObjectState> allObjects;
    private Dictionary<string, List<GameObjectState>> objectsInFieldOfView;



    public float fov = 50;
    public float maxdistance = 100;
    private float arenaSize = 80f;

    public GameSimulation(GameSimRules ruleset)
    {
        activePlayers = new List<Player>();
        rules = ruleset;
        tankFactory = new TankFactory();
        enqueuedCommands = new Queue<GameCommand>();
        tankControllers = new List<TankController>();
        allObjects = new List<GameObjectState>();
        objectsInFieldOfView = new Dictionary<string, List<GameObjectState>>();
       
    }

    internal GameObject CreatePlayer(PlayerCreate create)
    {
        //get a random point in the arena
        Vector3 potentialStartPoint = RandomArenaPosition();


        //TODO - check starting point for obstacles. Don't start too close to other tanks


        var t = tankFactory.CreateTank(create.Color, create.Name, create.Token, potentialStartPoint);
        //randomly rotate the tank
        t.GetComponent<TankController>().transform.Rotate(Vector3.up, UnityEngine.Random.Range(0, 360));
        t.GetComponent<TankController>().Ruleset = rules;
        t.GetComponent<TankController>().Sim = this;
        tankControllers.Add(t.GetComponent<TankController>());
        return t;
    }


    internal GameObject CreatePlayerTest(PlayerCreateTest create)
    {

        var t = tankFactory.CreateTank(create.Color, create.Name, create.Token, new Vector3(float.Parse(create.X), 5, float.Parse(create.Y)));

        t.GetComponent<TankController>().transform.Rotate(Vector3.up, float.Parse(create.Angle));

        t.GetComponent<TankController>().Ruleset = rules;
        t.GetComponent<TankController>().Sim = this;
        tankControllers.Add(t.GetComponent<TankController>());
        return t;
    }

    internal GameObject CreateAITank(string color, string name, Vector3 startingPos, bool infiniteHealth, bool infiniteAmmo)
    {
        var t = tankFactory.CreateAITank(color, name, startingPos);

        t.GetComponent<AITankController>().Ruleset = rules;
        t.GetComponent<AITankController>().Sim = this;
        t.GetComponent<AITankController>().infiniteAmmo = infiniteAmmo;
        t.GetComponent<AITankController>().infiniteHealth = infiniteHealth;
        tankControllers.Add(t.GetComponent<AITankController>());
        return t;
    }

    internal GameObject CreateDummyTank(string color, string name, Vector3 startingPos, bool infiniteHealth, bool infiniteAmmo)
    {
        var t = tankFactory.CreateDummyTank(color, name, startingPos);

        t.GetComponent<DummyTank>().Ruleset = rules;
        t.GetComponent<DummyTank>().Sim = this;
        t.GetComponent<DummyTank>().infiniteAmmo = infiniteAmmo;
        t.GetComponent<DummyTank>().infiniteHealth = infiniteHealth;
        tankControllers.Add(t.GetComponent<DummyTank>());
        return t;
    }

    private Vector3 RandomArenaPosition()
    {
        var randomCirclePoint = UnityEngine.Random.insideUnitCircle;

        //random start point
        Vector3 potentialStartPoint = new Vector3(randomCirclePoint.x, 0, randomCirclePoint.y);
        potentialStartPoint *= UnityEngine.Random.Range(0, arenaSize - 10);
        return potentialStartPoint;
    }

    public void Update()
    {
        allObjects.Clear();


        if (enqueuedCommands.Count > 0)
        {
            GameCommand command = enqueuedCommands.Dequeue();
            HandleCommand(command);
        }

        lock (allObjects)
        {
            UpdateTankState();
        }

        lock (objectsInFieldOfView)
        {
            var tanks = UnityEngine.GameObject.FindObjectsOfType<TankController>();
            foreach (TankController t in tanks)
            {

                UpdateTankViewObjects(t);

            }

        }
    }

    internal void RespawnTank(TankController tankController)
    {
        //get a random point in the arena
        Vector3 potentialStartPoint = RandomArenaPosition();
        tankController.transform.position = potentialStartPoint;

        tankController.transform.Rotate(Vector3.up, UnityEngine.Random.Range(0, 360));
        tankController.ReActivate();
    }

    private void UpdateTankViewObjects(TankController t)
    {
        var objectsToAdd = new List<GameObjectState>();
        var tanks = UnityEngine.GameObject.FindObjectsOfType<TankController>();
        foreach (TankController t2 in tanks)
        {

            //this is us, don't bother returning.
            if (t == t2)
                continue;

            if (t.turret == null)
                return;

            float distanceBetweenTanks = (t.transform.position - t2.transform.position).magnitude;




            Vector3 toTank = t2.transform.position - t.transform.position;
            toTank.Normalize();

            //turret is the wrong way round, so need to use up.
            float angle = Vector3.Angle(t.turret.transform.up, toTank);


            float dot = Vector3.Dot(t.turret.transform.up, toTank);

            if (distanceBetweenTanks < maxdistance && (Mathf.Abs(angle) < fov / 2))
            {
                if (dot > 0 && dot < 1)
                {

                    if (t.Name == "AITank1")
                    {
                        Debug.DrawLine(t.transform.position, t2.transform.position, Color.green, 0);
                        //Debug.Log("AITank1 sees : " + t2.Name + " distance: " + distanceBetweenTanks + " angle: " + angle + " dot: " + dot);
                    }

                    var obState = CreateTankState(t2);
                    objectsToAdd.Add(obState);
                }


            }



            objectsInFieldOfView[t.Token] = objectsToAdd;


            //Debug.Log(t.Name + " can see " + objectsInFieldOfView[t.Token].Count + " tanks ");

        }
    }

    private void UpdateTankState()
    {
        var tanks = UnityEngine.GameObject.FindObjectsOfType<TankController>();
        foreach (TankController t in tanks)
        {
            GameObjectState s = CreateTankState(t);
            allObjects.Add(s);


        }
    }

    internal void RecordFrag(TankController victim, TankController killer)
    {
        Debug.Log(victim.Name + " killed by " + killer.Name);
        victim.Deaths++;
        killer.Kills++;

    }

    private static GameObjectState CreateTankState(TankController t)
    {
        GameObjectState s = new GameObjectState();
        s.Ammo = t.Ammo;
        s.Heading = t.Heading;
        s.Health = t.Health;
        s.Name = t.Name;
        s.Type = "Tank";
        s.X = t.X;
        s.Y = t.Y;
        s.TurretHeading = t.TurretHeading;
        return s;
    }

    private void HandleCommand(GameCommand command)
    {
        TankController t = FindTankObject(command.Token);



        switch (command.Type)
        {
            case (CommandType.PlayerCreate):

                PlayerCreate create = command.Payload as PlayerCreate;
                var tank = CreatePlayer(create);
                break;

            case (CommandType.Despawn):

                if (t != null)
                    RemoveTank(t);

                break;

            case (CommandType.PlayerCreateTest):

                PlayerCreateTest createtest = command.Payload as PlayerCreateTest;
                var tanktest = CreatePlayerTest(createtest);
                break;

            case (CommandType.Forward):
                if (t != null)
                    t.ToggleForward();
                break;
            case (CommandType.Reverse):
                if (t != null)
                    t.ToggleReverse();
                break;
            case (CommandType.Right):
                if (t != null)
                    t.ToggleRight();
                break;
            case (CommandType.Left):
                if (t != null)
                    t.ToggleLeft();
                break;
            case (CommandType.TurretLeft):
                if (t != null)
                    t.ToggleTurretLeft();
                break;
            case (CommandType.TurretRight):
                if (t != null)
                    t.ToggleTurretRight();
                break;
            case (CommandType.Stop):
                if (t != null)
                    t.Stop();
                break;
            case (CommandType.StopTurret):
                if (t != null)
                    t.StopTurret();
                break;
            case (CommandType.Fire):
                if (t != null)
                    t.Fire();
                break;
        }
    }

    private void RemoveTank(TankController t)
    {
        tankControllers.Remove(t);
        objectsInFieldOfView.Remove(t.Token);
        GameObject.Destroy(t.gameObject);
    }

    public TankController FindTankObject(string token)
    {
        var tanks = UnityEngine.GameObject.FindObjectsOfType<TankController>();
        foreach (TankController t in tanks)
        {
            if (t.Token == token)
                return t;
        }
        return null;
    }

    internal List<GameObjectState> GetObjectsInViewOfTank(string token)
    {
        lock (objectsInFieldOfView)
        {
            if (objectsInFieldOfView.Count > 0)
                if (objectsInFieldOfView.ContainsKey(token))
                    return objectsInFieldOfView[token];

        }
        return new List<GameObjectState>();
    }
}

public class GameSimRules
{
    public int FragWinLimit { get; set; }
    public int RespawnTime { get; set; }
    public int GameTimeLimit { get; set; }
    public bool TrainingMode { get; set; }

    public GameSimRules()
    {
        FragWinLimit = 10;
        RespawnTime = 5;
        GameTimeLimit = 300;
        TrainingMode = false;
    }
}