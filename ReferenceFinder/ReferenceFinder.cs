using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
//using ResoniteHotReloadLib;
using ResoniteModLoader;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ReferenceFinderMod
{
	public class ReferenceFinderMod : ResoniteMod
	{
		public override string Name => "Reference Finder Wizard";
		public override string Author => "Nytra";
		public override string Version => "1.0.2";
		public override string Link => "https://github.com/Nytra/ResoniteReferenceFinderWizard";

		const string WIZARD_TITLE = "Reference Finder Wizard (Mod)";

		public override void OnEngineInit()
		{
			//HotReloader.RegisterForHotReload(this);
			Engine.Current.RunPostInit(AddMenuOption);
		}
		static void AddMenuOption()
		{
			DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => ReferenceFinderWizard.GetOrCreateWizard(x));
		}

		//static void BeforeHotReload()
		//{
		//	HotReloader.RemoveMenuOption("Editor", WIZARD_TITLE);
		//}

		//static void OnHotReload(ResoniteMod modInstance)
		//{
		//	AddMenuOption();
		//}

		class ReferenceFinderWizard
		{
			public static ReferenceFinderWizard GetOrCreateWizard(Slot x)
			{
				return new ReferenceFinderWizard(x);
			}

			Slot WizardSlot;

			readonly ReferenceField<IWorldElement> elementField;

			readonly ValueField<bool> includeChildrenSlots;
			readonly ValueField<bool> includeChildrenMembers;
			readonly ValueField<bool> includeChildrenComponents;

			readonly ValueField<bool> ignoreNonPersistent;
			readonly ValueField<bool> ignoreSelfReferences;
			readonly ValueField<bool> ignoreInspectors;
			readonly ValueField<bool> ignoreNonWorkerRefs;
			readonly ValueField<bool> ignoreNestedRefs;
			//readonly ValueField<bool> ignoreNonSlotRefs;

			readonly ValueField<bool> showDetails;
			readonly ValueField<bool> showElementType;
			readonly ValueField<bool> showLabels;

			readonly ValueField<int> maxResults;

			readonly ReferenceMultiplexer<ISyncRef> results;

			readonly Dictionary<IWorldElement, HashSet<ISyncRef>> referenceMap = new();

			static FieldInfo objectsField = AccessTools.Field(typeof(ReferenceController), "objects");

			readonly Button searchButton;

			bool performingOperations = false;

			readonly Text statusText;
			void UpdateStatusText(string info)
			{
				statusText.Content.Value = info;
			}

			string GetNiceElementName(IWorldElement element)
			{
				if (element == null)
				{
					return "<null element>";
				}
				string s = null;
				if (element is User user)
				{
					s = "User " + user.UserName;
				}
				else if (element is SyncElement syncElement)
				{
					s = syncElement.NameWithPath;
				}
				else if (element is Component component)
				{
					//s = component.Name;
					s = component.GetType().GetNiceName();
				}
				else if (element is Slot slot)
				{
					s = StripTags(slot.Name);
				}
                else
                {
					s = element.Name;
                }
				if (string.IsNullOrEmpty(s))
				{
					s = element.GetType().Name;
				}
                return s + $" <color=gray>({element.ReferenceID})</color>";
			}

			string GetElementParentHierarchyString(IWorldElement element, bool reverse=true)
			{
				string nameOverride = null;
				if (element is User user)
				{
					nameOverride = user.UserName;
				}

				string str;
				List<IWorldElement> parents = new List<IWorldElement>();

				IWorldElement parent = element.Parent?.FilterWorldElement();
				while (parent != null && parent is not World)
				{
					parents.Add(parent);
					parent = parent.Parent?.FilterWorldElement();
				}

				if (reverse)
				{
					str = "";
					parents.Reverse();
					bool first = true;
					foreach (IWorldElement parentElem in parents)
					{
						if (first)
						{
							str += parentElem.Name;
							first = false;
						}
						else
						{
							str += "/" + parentElem.Name;
						}
					}
					if (first)
					{
						str += nameOverride ?? element.Name;
						//first = false;
					}
					else
					{
						str += "/" + (nameOverride ?? element.Name);
					}
				}
				else
				{
					str = nameOverride ?? element.Name;
					foreach (IWorldElement parentElem in parents)
					{
						str += "/" + parentElem.Name;
					}
				}

				return str;
			}

			bool ValidateWizard()
			{
				if (elementField.Reference.Target == null)
				{
					UpdateStatusText("No element provided!");
					return false;
				}

				if (performingOperations)
				{
					UpdateStatusText("Operations in progress! (Or the mod has crashed)");
					return false;
				}

				return true;
			}

			ReferenceFinderWizard(Slot x)
			{
				WizardSlot = x;
				WizardSlot.Tag = "Developer";
				WizardSlot.PersistentSelf = false;
				WizardSlot.LocalScale *= 0.0008f;

				Slot Data = WizardSlot.AddSlot("Data");
				elementField = Data.AddSlot("elementField").AttachComponent<ReferenceField<IWorldElement>>();
				includeChildrenMembers = Data.AddSlot("includeChildrenMembers").AttachComponent<ValueField<bool>>();
				//includeChildrenMembers.Value.Value = true;
				includeChildrenSlots = Data.AddSlot("includeChildrenSlots").AttachComponent<ValueField<bool>>();
				//includeChildrenSlots.Value.Value = true;
				includeChildrenComponents = Data.AddSlot("includeChildrenSlots").AttachComponent<ValueField<bool>>();
				//includeChildrenComponents.Value.Value = true;
				ignoreNonPersistent = Data.AddSlot("ignoreNonPersistent").AttachComponent<ValueField<bool>>();
				//ignoreNonPersistent.Value.Value = true;
				ignoreSelfReferences = Data.AddSlot("ignoreSelfReferences").AttachComponent<ValueField<bool>>();
				//ignoreSelfReferences.Value.Value = true;
				ignoreInspectors = Data.AddSlot("ignoreInspectors").AttachComponent<ValueField<bool>>();
				ignoreInspectors.Value.Value = true;
				ignoreNonWorkerRefs = Data.AddSlot("ignoreNonWorkerRefs").AttachComponent<ValueField<bool>>();
				ignoreNonWorkerRefs.Value.Value = true;
				ignoreNestedRefs = Data.AddSlot("ignoreNestedRefs").AttachComponent<ValueField<bool>>();
				ignoreNestedRefs.Value.Value = true;
				//ignoreNonSlotRefs = Data.AddSlot("ignoreNonSlotRefs").AttachComponent<ValueField<bool>>();
				//ignoreNonSlotRefs.Value.Value = true;
				showDetails = Data.AddSlot("showDetails").AttachComponent<ValueField<bool>>();
				showElementType = Data.AddSlot("showElementType").AttachComponent<ValueField<bool>>();
				showLabels = Data.AddSlot("showLabels").AttachComponent<ValueField<bool>>();
				maxResults = Data.AddSlot("maxResults").AttachComponent<ValueField<int>>();
				maxResults.Value.Value = 256;
				results = Data.AddSlot("results").AttachComponent<ReferenceMultiplexer<ISyncRef>>();

				UIBuilder UI = RadiantUI_Panel.SetupPanel(WizardSlot, WIZARD_TITLE.AsLocaleKey(), new float2(1000f, 756f));
				RadiantUI_Constants.SetupEditorStyle(UI);

				UI.Canvas.MarkDeveloper();
				UI.Canvas.AcceptPhysicalTouch.Value = false;

				UI.SplitHorizontally(0.35f, out RectTransform left, out RectTransform right);

				left.OffsetMax.Value = new float2(-2f);
				right.OffsetMin.Value = new float2(2f);

				UI.NestInto(left);

				VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopCenter);
				verticalLayout.ForceExpandHeight.Value = false;

				UI.Style.MinHeight = 24f;
				UI.Style.PreferredHeight = 24f;
				UI.Style.PreferredWidth = 400f;
				UI.Style.MinWidth = 400f;

				UI.Text("Drop any world element here:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
				UI.Next("Element");
				UI.Current.AttachComponent<RefEditor>().Setup(elementField.Reference);

				UI.HorizontalElementWithLabel("Include child slots if the element is a slot:", 0.942f, () => UI.BooleanMemberEditor(includeChildrenSlots.Value));

				UI.HorizontalElementWithLabel("Include contained components:", 0.942f, () => UI.BooleanMemberEditor(includeChildrenComponents.Value));

				UI.HorizontalElementWithLabel("Include contained members:", 0.942f, () => UI.BooleanMemberEditor(includeChildrenMembers.Value));

				UI.Spacer(24f);

				UI.HorizontalElementWithLabel("Ignore references from inside inspector UI:", 0.942f, () => UI.BooleanMemberEditor(ignoreInspectors.Value));

				UI.HorizontalElementWithLabel("Ignore nested references:", 0.942f, () => UI.BooleanMemberEditor(ignoreNestedRefs.Value));

				UI.HorizontalElementWithLabel("Ignore references which cannot be opened in inspectors:", 0.942f, () => UI.BooleanMemberEditor(ignoreNonWorkerRefs.Value));

				//UI.HorizontalElementWithLabel("Ignore references which don't exist on slots:", 0.942f, () => UI.BooleanMemberEditor(ignoreNonSlotRefs.Value));

				UI.HorizontalElementWithLabel("Ignore references which are children of the search element:", 0.942f, () => UI.BooleanMemberEditor(ignoreSelfReferences.Value));

				UI.HorizontalElementWithLabel("Ignore non-persistent references:", 0.942f, () => UI.BooleanMemberEditor(ignoreNonPersistent.Value));

				UI.Spacer(24f);

				UI.HorizontalElementWithLabel("Max results:", 0.884f, () =>
				{
					var intField = UI.IntegerField(1, 1024);
					intField.ParsedValue.Value = maxResults.Value.Value;
					intField.ParsedValue.OnValueChange += (field) => maxResults.Value.Value = field.Value;
					return intField;
				});

				UI.HorizontalElementWithLabel("Spawn detail text:", 0.942f, () => UI.BooleanMemberEditor(showDetails.Value));

				UI.HorizontalElementWithLabel("Show element type in detail text:", 0.942f, () => UI.BooleanMemberEditor(showElementType.Value));

				//UI.HorizontalElementWithLabel("Show slot, component and user labels in detail text:", 0.942f, () => UI.BooleanMemberEditor(showLabels.Value));

				searchButton = UI.Button("Search");
				searchButton.LocalPressed += SearchPressed;

				UI.Spacer(24f);

				UI.Text("Status:");
				statusText = UI.Text("---");

				UI.NestInto(right);
				UI.ScrollArea();
				UI.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

				SyncMemberEditorBuilder.Build(results.References, "References", null, UI);

				WizardSlot.PositionInFrontOfUser(float3.Backward, distance: 1f);
			}

			bool isElementPersistent(IWorldElement element)
			{
				if (element.IsPersistent)
				{
					if (element.Parent != null)
					{
						return isElementPersistent(element.Parent);
					}
					else
					{
						return true;
					}
				}
				return false;
			}

			void SearchPressed(IButton button, ButtonEventData eventData)
			{
				if (!ValidateWizard()) return;

				performingOperations = true;
				searchButton.Enabled = false;

				results.References.Clear();

				if (referenceMap.Count > 0)
				{
					referenceMap.Clear();
				}

				var elements = new HashSet<IWorldElement>();

				GetSearchElements(elements, elementField.Reference.Target);

				FindReferences(elements, out bool stoppedEarly);

				if (stoppedEarly)
				{
					UpdateStatusText($"Found {results.References.Count} references (Max Results limit reached).");
				}
				else
				{
					UpdateStatusText($"Found {results.References.Count} references.");
				}

				if (showDetails.Value.Value && results.References.Count > 0)
				{
					Slot textSlot = WizardSlot.LocalUserSpace.AddSlot("Detail Text");
					UniversalImporter.SpawnText(textSlot, "Detail Text", GetAllText(), textSize: 12, canvasSize: new float2(1200, 400));
					textSlot.PositionInFrontOfUser();
				}

				if (referenceMap.Count > 0)
				{
					referenceMap.Clear();
				}

				performingOperations = false;
				searchButton.Enabled = true;
			}

			void GetSearchElements(HashSet<IWorldElement> elements, IWorldElement target)
			{
				if (target.FilterWorldElement() == null || target.IsLocalElement) return;

				elements.Add(target);

				// Children slots
				if (includeChildrenSlots.Value && target is Slot slot)
				{
					foreach (Slot childSlot in slot.Children)
					{
						GetSearchElements(elements, childSlot);
					}
				}

				// Sync members
				if (includeChildrenMembers.Value && target is Worker worker)
				{
					foreach (ISyncMember syncMember in worker.SyncMembers)
					{
						GetSearchElements(elements, syncMember);
					}
				}

				// Slot components
				if (includeChildrenComponents.Value && target is ContainerWorker<Component> containerWorker)
				{
					foreach (Component component in containerWorker.Components)
					{
						GetSearchElements(elements, component);
					}
				}
				// User components
				else if (includeChildrenComponents.Value && target is ContainerWorker<UserComponent> containerWorker2)
				{
					foreach (UserComponent userComponent in containerWorker2.Components)
					{
						GetSearchElements(elements, userComponent);
					}
				}

				// syncBags
				if (includeChildrenMembers.Value && target is ISyncBag syncBag)
				{
					foreach (IWorldElement element in syncBag.Values)
					{
						GetSearchElements(elements, element);
					}
				}

				// syncLists
				if (includeChildrenMembers.Value && target is ISyncList syncList)
				{
					if (syncList.Count > 0 && syncList.GetElement(0) is IWorldElement)
					{
						foreach (IWorldElement element in syncList.Elements)
						{
							GetSearchElements(elements, element);
						}
					}
				}

				// syncArrays
				if (includeChildrenMembers.Value && target is ISyncArray syncArray)
				{
					if (syncArray.Count > 0 && syncArray.GetElement(0) is IWorldElement)
					{
						for (int i = 0; i < syncArray.Count; i++)
						{
							GetSearchElements(elements, (IWorldElement)syncArray.GetElement(i));
						}
					}
				}

				// syncDictionaries
				if (includeChildrenMembers.Value && target is ISyncDictionary syncDict)
				{
					foreach (ISyncMember element in syncDict.Values)
					{
						GetSearchElements(elements, element);
					}
				}

				// syncVars
				if (includeChildrenMembers.Value && target is SyncVar syncVar && syncVar.Element != null)
				{
					GetSearchElements(elements, syncVar.Element);
				}
			}

			bool IsInInspector(IWorldElement element)
			{
				Slot s = element.FindNearestParent<Slot>();
				if (s != null)
				{
					return s.GetComponentInParents<WorkerInspector>() != null || s.GetComponentInParents<SceneInspector>() != null || s.GetComponentInParents<UserInspector>() != null;
				}
				return false;
			}

			bool IsOpenableInInspector(IWorldElement element)
			{
				if (element.FindNearestParent<Worker>().FilterWorldElement() == null) return false;
				return true;
			}

			void ProcessReference(HashSet<IWorldElement> elements, ISyncRef syncRef)
			{
				if (syncRef.FilterWorldElement() != null
					&& syncRef.Target?.FilterWorldElement() != null
					&& elements.Contains(syncRef.Target)
					&& !syncRef.Target.IsLocalElement
					&& !syncRef.IsLocalElement
					&& syncRef != elementField.Reference
					&& syncRef.Parent != results.References
					&& !(ignoreNonPersistent.Value && !isElementPersistent(syncRef))
					&& !(ignoreSelfReferences.Value && syncRef.IsChildOfElement(elementField.Reference.Target))
					&& !(ignoreInspectors.Value && IsInInspector(syncRef))
					&& !(ignoreNonWorkerRefs.Value && !IsOpenableInInspector(syncRef))
					// ignore ISyncRefs which are children of other ISyncRefs? (avoids having two results which basically point to the same thing e.g. User field in UserRef sync object on VUO)
					&& !(ignoreNestedRefs.Value && syncRef.Parent?.FindNearestParent<ISyncRef>()?.FilterWorldElement() != null))
				{
					results.References.Add(syncRef);
					if (showDetails.Value)
					{
						if (!referenceMap.ContainsKey(syncRef.Target))
						{
							referenceMap.Add(syncRef.Target, new HashSet<ISyncRef>());
						}
						referenceMap[syncRef.Target].Add(syncRef);
					}
				}
			}

			void FindReferences(HashSet<IWorldElement> elements, out bool stoppedEarly)
			{
				stoppedEarly = false;
				var objectsDict = (Dictionary<RefID, IWorldElement>)objectsField.GetValue(WizardSlot.World.ReferenceController);
				if (objectsDict != null)
				{
					foreach (IWorldElement element in objectsDict.Values.ToList())
					{
						if (element is ISyncRef syncRef)
						{
							if (results.References.Count >= maxResults.Value.Value)
							{
								stoppedEarly = true;
								break;
							}
							ProcessReference(elements, syncRef);
						}
					}
				}
				else
				{
					Error("Could not find world objects dictionary!");
					UpdateStatusText("Error: Could not find world objects dictionary!");
				}

				// using World.ForeachWorldElement does not get every single reference because it only searches slots
				// so it doesn't find refs on things like the permission controller which doesn't exist on a slot

				//bool stoppedEarlyInternal = false;
				//WizardSlot.World.ForeachWorldElement((ISyncRef syncRef) =>
				//{
				//	if (results.References.Count >= maxResults.Value.Value)
				//	{
				//		// why can't i use the out bool in here... :(
				//		stoppedEarlyInternal = true;
				//		return;
				//	}
				//	ProcessReference(elements, syncRef);
				//});
			}

			string GetAllText()
			{
				string text = "";
				foreach (IWorldElement element in referenceMap.Keys)
				{
					text += GetElementText(element, showElementType.Value, showLabels.Value) + $" {GetSlotOrUserPathText(GetNearestParentSlotOrUser(element))} is referenced by:\n";
					foreach (ISyncRef syncRef in referenceMap[element])
					{
						text += "  • " + GetElementText(syncRef, showElementType.Value, showLabels.Value) + $" {GetSlotOrUserPathText(GetNearestParentSlotOrUser(syncRef))}\n";
					}
					text += "\n";
				}

				return text;
			}

			IWorldElement GetNearestParentSlotOrUser(IWorldElement element)
			{
				return (IWorldElement)element.FindNearestParent<Slot>() ?? (IWorldElement)element.FindNearestParent<User>();
			}

			string GetSlotOrUserPathText(IWorldElement element)
			{
				if (element.FilterWorldElement() == null)
				{
					return "";
				}
				string pathText;
				string color = "hero.cyan";

				if (element is Slot)
				{
					//color = "hero.yellow";
					pathText = StripTags(GetElementParentHierarchyString(element));
				}
				else
				{
					pathText = GetElementParentHierarchyString(element);
				}

				if (element is User)
				{
					color = "hero.orange";
				}

				return $"<color={color}>(" + pathText + ")</color>";
			}

			string StripTags(string s)
			{
				if (string.IsNullOrEmpty(s))
				{
					return s;
				}
				return new StringRenderTree(s).GetRawString();
			}

			// Based on code from RefEditor.OnChanges
			string GetElementText(IWorldElement element, bool showElementType = false, bool showLabels = false)
			{
				Component component = element.FindNearestParent<Component>();
				Slot slot = component?.Slot ?? element.FindNearestParent<Slot>();

				string value;
				if (element is Slot slot2)
				{
					value = $"<color=hero.yellow>{(showLabels ? "Slot: " : "")}{GetNiceElementName(slot2)}</color>";
					return value;
				}
				else
				{
					string arg;
					if (component != null && component != element)
					{
						arg = $"on <color=hero.purple>{(showLabels ? "Component: " : "")}" + GetNiceElementName(component) + $"</color> on <color=hero.yellow>{(showLabels ? "Slot: " : "")}" + GetNiceElementName(slot) + "</color>";
					}
					else
					{
						arg = (slot == null) ? "" : ($"on <color=hero.yellow>{(showLabels ? "Slot: " : "")}" + GetNiceElementName(slot) + "</color>");
					}

					string elemPrefix = "<color=hero.green>";
					string elemPostfix = "</color>";
					if (element is Component)
					{
						elemPrefix = $"<color=hero.purple>{(showLabels ? "Component: " : "")}";
					}
					else if (element is User)
					{
						elemPrefix = $"<color=hero.orange>{(showLabels ? "User: " : "")}";
					}
					else if (showElementType)
					{
						elemPrefix += "<color= #28b463 >" + element.GetType().GetNiceName() + ": " + "</color>";
					}

					value = $"{elemPrefix}{GetNiceElementName(element)}{elemPostfix} {arg}";

					return value;
				}
			}
		}
	}
}