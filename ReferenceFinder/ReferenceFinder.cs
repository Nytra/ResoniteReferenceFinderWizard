using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ResoniteHotReloadLib;
using ResoniteModLoader;
using System.Collections.Generic;

namespace ReferenceFinder
{
	public class ReferenceFinder : ResoniteMod
	{
		public override string Name => "Reference Finder Wizard";
		public override string Author => "Nytra";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/Nytra/ResoniteReferenceFinderWizard";

		const string WIZARD_TITLE = "Reference Finder Wizard (Mod)";

		public override void OnEngineInit()
		{
			HotReloader.RegisterForHotReload(this);
			Engine.Current.RunPostInit(AddMenuOption);
		}
		static void AddMenuOption()
		{
			DevCreateNewForm.AddAction("Editor", WIZARD_TITLE, (x) => ReferenceFinderWizard.GetOrCreateWizard(x));
		}

		static void BeforeHotReload()
		{
			HotReloader.RemoveMenuOption("Editor", WIZARD_TITLE);
		}

		static void OnHotReload(ResoniteMod modInstance)
		{
			AddMenuOption();
		}

		class ReferenceFinderWizard
		{
			public static ReferenceFinderWizard GetOrCreateWizard(Slot x)
			{
				return new ReferenceFinderWizard(x);
			}

			Slot WizardSlot;

			readonly ReferenceField<IWorldElement> elementField;

			readonly ValueField<bool> processWorkerSyncMembers;
			readonly ValueField<bool> processContainedComponents;
			readonly ValueField<bool> processChildrenSlots;
			readonly ValueField<bool> ignoreNonPersistent;
			readonly ValueField<bool> ignoreSelfReferences;
			//readonly ValueField<bool> ignoreSlotParentRef;
			readonly ValueField<bool> showDetails;
			readonly ValueField<int> maxResults;

			readonly ReferenceMultiplexer<ISyncRef> results;

			readonly Button searchButton;

			bool performingOperations = false;

			readonly Text statusText;
			void UpdateStatusText(string info)
			{
				statusText.Content.Value = info;
			}

			string GetSlotParentHierarchyString(Slot slot, bool reverse = true)
			{
				string str;
				List<Slot> parents = new List<Slot>();

				slot.ForeachParent((parent) =>
				{
					parents.Add(parent);
				});

				if (reverse)
				{
					str = "";
					parents.Reverse();
					bool first = true;
					foreach (Slot s in parents)
					{
						if (first)
						{
							str += s.Name;
							first = false;
						}
						else
						{
							str += "/" + s.Name;
						}
					}
					if (first)
					{
						str += slot.Name;
						first = false;
					}
					else
					{
						str += "/" + slot.Name;
					}

				}
				else
				{
					str = slot.Name;
					foreach (Slot s in parents)
					{
						str += "/" + s.Name;
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
				processWorkerSyncMembers = Data.AddSlot("processWorkerSyncMembers").AttachComponent<ValueField<bool>>();
				processWorkerSyncMembers.Value.Value = true;
				processContainedComponents = Data.AddSlot("processContainedComponents").AttachComponent<ValueField<bool>>();
				processContainedComponents.Value.Value = true;
				processChildrenSlots = Data.AddSlot("processChildrenSlots").AttachComponent<ValueField<bool>>();
				processChildrenSlots.Value.Value = true;
				ignoreNonPersistent = Data.AddSlot("ignoreNonPersistent").AttachComponent<ValueField<bool>>();
				ignoreSelfReferences = Data.AddSlot("ignoreSelfReferences").AttachComponent<ValueField<bool>>();
				//ignoreSlotParentRef = Data.AddSlot("ignoreSlotParentRef").AttachComponent<ValueField<bool>>();
				showDetails = Data.AddSlot("showDetails").AttachComponent<ValueField<bool>>();
				maxResults = Data.AddSlot("maxResults").AttachComponent<ValueField<int>>();
				maxResults.Value.Value = 256;
				results = Data.AddSlot("results").AttachComponent<ReferenceMultiplexer<ISyncRef>>();

				UIBuilder UI = RadiantUI_Panel.SetupPanel(WizardSlot, WIZARD_TITLE.AsLocaleKey(), new float2(800f, 756f));
				RadiantUI_Constants.SetupEditorStyle(UI);

				UI.Canvas.MarkDeveloper();
				UI.Canvas.AcceptPhysicalTouch.Value = false;

				UI.SplitHorizontally(0.5f, out RectTransform left, out RectTransform right);

				left.OffsetMax.Value = new float2(-2f);
				right.OffsetMin.Value = new float2(2f);

				UI.NestInto(left);

				VerticalLayout verticalLayout = UI.VerticalLayout(4f, childAlignment: Alignment.TopCenter);
				verticalLayout.ForceExpandHeight.Value = false;

				UI.Style.MinHeight = 24f;
				UI.Style.PreferredHeight = 24f;
				UI.Style.PreferredWidth = 400f;
				UI.Style.MinWidth = 400f;

				UI.Text("Element:").HorizontalAlign.Value = TextHorizontalAlignment.Left;
				UI.Next("Element");
				UI.Current.AttachComponent<RefEditor>().Setup(elementField.Reference);

				UI.HorizontalElementWithLabel("Process children slots:", 0.942f, () => UI.BooleanMemberEditor(processChildrenSlots.Value));

				UI.HorizontalElementWithLabel("Process contained components:", 0.942f, () => UI.BooleanMemberEditor(processContainedComponents.Value));

				UI.HorizontalElementWithLabel("Process sync members:", 0.942f, () => UI.BooleanMemberEditor(processWorkerSyncMembers.Value));

				UI.HorizontalElementWithLabel("Ignore references which are children of the element:", 0.942f, () => UI.BooleanMemberEditor(ignoreSelfReferences.Value));

				//UI.HorizontalElementWithLabel("Ignore slot parent references:", 0.942f, () => UI.BooleanMemberEditor(ignoreSlotParentRef.Value));

				UI.HorizontalElementWithLabel("Ignore non-persistent references:", 0.942f, () => UI.BooleanMemberEditor(ignoreNonPersistent.Value));

				UI.Spacer(24f);

				UI.HorizontalElementWithLabel("Max Results:", 0.884f, () =>
				{
					var intField = UI.IntegerField(1, 1024);
					intField.ParsedValue.Value = maxResults.Value.Value;
					intField.ParsedValue.OnValueChange += (field) => maxResults.Value.Value = field.Value;
					return intField;
				});

				UI.HorizontalElementWithLabel("Spawn Detail Text:", 0.942f, () => UI.BooleanMemberEditor(showDetails.Value));

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

				performingOperations = false;
				searchButton.Enabled = true;
			}

			void GetSearchElements(HashSet<IWorldElement> elements, IWorldElement target)
			{
				elements.Add(target);
				if (processChildrenSlots.Value && target is Slot slot)
				{
					foreach (Slot childSlot in slot.Children)
					{
						GetSearchElements(elements, childSlot);
					}
				}
				if (processWorkerSyncMembers.Value && target is Worker worker)
				{
					foreach (ISyncMember syncMember in worker.SyncMembers)
					{
						GetSearchElements(elements, syncMember);
					}
				}
				if (processContainedComponents.Value && target is ContainerWorker<Component> containerWorker)
				{
					foreach (Component component in containerWorker.Components)
					{
						GetSearchElements(elements, component);
					}
				}
				else if (processContainedComponents.Value && target is ContainerWorker<UserComponent> containerWorker2)
				{
					foreach (UserComponent userComponent in containerWorker2.Components)
					{
						GetSearchElements(elements, userComponent);
					}
				}
			}

			void FindReferences(HashSet<IWorldElement> elements, out bool stoppedEarly)
			{
				bool stoppedEarlyInternal = false;
				WizardSlot.World.ForeachWorldElement((ISyncRef syncRef) =>
				{
					if (results.References.Count >= maxResults.Value.Value)
					{
						stoppedEarlyInternal = true;
						return;
					}
					if (elements.Contains(syncRef.Target)
						&& !syncRef.Target.IsLocalElement
						&& !syncRef.IsLocalElement
						&& syncRef != elementField.Reference
						&& syncRef.Parent != results.References
						&& !(ignoreNonPersistent.Value && !isElementPersistent(syncRef))
						//&& !(ignoreSlotParentRef.Value && (syncRef.Parent is Slot s && s.ParentReference == syncRef))
						&& !(ignoreSelfReferences.Value && syncRef.IsChildOfElement(elementField.Reference.Target)))
					{
						results.References.Add(syncRef);
					}
				});
				stoppedEarly = stoppedEarlyInternal;
			}

			string GetAllText()
			{
				string text = "";
				foreach (ISyncRef element in results.References)
				{
					text += GetText(element);
				}
				return text;
			}

			string GetText(ISyncRef syncRef)
			{
				string pathText = $"{GetSlotParentHierarchyString(syncRef.FindNearestParent<Slot>())}";
				return "* " + GetElementText(syncRef.Target) + " is referenced by " + GetElementText(syncRef) + $" (<color=hero.cyan>{StripTags(pathText)}</color>)" + "\n";
				//return GetElementText(syncRef.Target, showParent: true) + " referenced by " + GetElementText(syncRef, showParent: true) + " on " + $"<color=hero.cyan>{GetSlotParentHierarchyString(syncRef.FindNearestParent<Slot>())}</color>" + "\n";
			}

			string StripTags(string s)
			{
				if (string.IsNullOrEmpty(s))
				{
					return s;
				}
				return new StringRenderTree(s).GetRawString();
			}

			string GetElementText(IWorldElement element, bool showElementType = false, bool showLabels = false)
			{
				//string typeText = $"<color=hero.yellow>{element.GetType().GetNiceName()}</color>";
				//string nameText = $"Element: <color=hero.green>{element.Name}</color>";
				//string parentText = $"Parent: <color=hero.purple>{element.Parent.Name}</color>";
				//return (showType ? typeText + " " : "") + nameText + (showParent ? " " + parentText : "");
				//return (showType ? typeText + " " : "" + (showParent ? parentText + "." : "") + nameText);

				Component component = element.FindNearestParent<Component>();
				Slot slot = component?.Slot ?? element.FindNearestParent<Slot>();
				string value;
				if (element is Slot slot2)
				{
					value = $"<color=hero.yellow>{(showLabels ? "Slot: " : "")}{StripTags(slot2.Name)}</color>";
					return value;
				}
				else
				{
					string arg = ((component != null && component != element) ? ($"on <color=hero.purple>{(showLabels ? "Component: " : "")}" + component.Name + $"</color> on <color=hero.yellow>{(showLabels ? "Slot: " : "")}" + StripTags(slot.Name) + "</color>") : ((slot == null) ? "" : ($"on <color=hero.yellow>{(showLabels ? "Slot: " : "")}" + StripTags(slot.Name) + "</color>")));
					string elemPrefix = "<color=hero.green>";
					if (element is Component component2)
					{
						elemPrefix = $"<color=hero.purple>{(showLabels ? "Component: " : "")}";
					}
					else if (showElementType)
					{
						elemPrefix += element.GetType().GetNiceName() + ": ";
					}
					value = ((!(element is SyncElement syncElement)) ? $"{elemPrefix}{element.Name ?? element.GetType().Name}</color> {arg}" : $"{elemPrefix}{syncElement.NameWithPath}</color> {arg}");
					return value;
				}
			}
		}
	}
}